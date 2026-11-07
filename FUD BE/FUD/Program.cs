using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using FUD.Common;
using FUD.Infrastructure.Auth;
using FUD.Infrastructure.Email;
using FUD.Models;
using FUD.Repositories;
using FUD.Repositories.Facilities;
using FUD.Repositories.Interfaces;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services;
using FUD.Services.Facilities;
using FUD.Services.Interfaces;
using FUD.Services.Interfaces.Facilities;
using FUD.Settings.pattern;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

// 0. Load biến môi trường từ file .env trước khi khởi tạo WebApplicationBuilder
var currentDirEnv = Path.Combine(Directory.GetCurrentDirectory(), ".env");

var baseDirEnv = Path.Combine(AppContext.BaseDirectory, ".env");
if (File.Exists(currentDirEnv))
{
    DotNetEnv.Env.Load(currentDirEnv);
}
else if (File.Exists(baseDirEnv))
{
    DotNetEnv.Env.Load(baseDirEnv);
}
else
{
    DotNetEnv.Env.TraversePath().Load();
}

var builder = WebApplication.CreateBuilder(args);

// Đảm bảo các biến môi trường từ .env được nạp và ghi đè cấu hình trong appsettings.json
builder.Configuration.AddEnvironmentVariables();

// 1. CSDL
builder.Services.AddDbContext<FudContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. In-Memory Cache (Đệm OnTokenValidated, tránh nghẽn DB)
builder.Services.AddMemoryCache();

// 3. Validate Cấu hình bảo mật khi Startup (Fail Fast)
var jwtKeyStr = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKeyStr))
    throw new InvalidOperationException("Cấu hình 'Jwt:Key' không được để trống!");

byte[] jwtKeyBytes;
try
{
    jwtKeyBytes = Convert.FromBase64String(jwtKeyStr);
}
catch (FormatException)
{
    jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKeyStr);
}

if (jwtKeyBytes.Length < 32)
    throw new InvalidOperationException($"'Jwt:Key' phải có độ dài tối thiểu 256 bits (32 bytes). Hiện tại: {jwtKeyBytes.Length} bytes.");

var googleClientId = builder.Configuration["Google:ClientId"];
if (string.IsNullOrWhiteSpace(googleClientId))
{
    throw new InvalidOperationException("Cấu hình 'Google:ClientId' không được để trống!");
}
if (!builder.Environment.IsDevelopment() && googleClientId.StartsWith("mock-", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Google:ClientId chưa được cấu hình hợp lệ trên Production!");
}

// 4. Rate Limiter Phân Vùng Theo IP
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("LoginPolicy", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.AddPolicy("RefreshPolicy", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

// 5. CORS (Hỗ trợ cấu hình mảng trong appsettings hoặc chuỗi trong .env)
var corsOriginsConfig = builder.Configuration["Cors:AllowedOrigins"];
string[] allowedOrigins;
if (!string.IsNullOrWhiteSpace(corsOriginsConfig))
{
    allowedOrigins = corsOriginsConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
else
{
    allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                     ?? new[] { "http://localhost:5173" };
}


// ── Facilities: UC28 Equipment & UC29 News ─────────────────
builder.Services.AddScoped<IEquipmentRepository, EquipmentRepository>();
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<INewsRepository, NewsRepository>();
builder.Services.AddScoped<INewsService, NewsService>();
// UC12: Pricing Settings
builder.Services.AddScoped<IPricingRepository, PricingRepository>();
builder.Services.AddScoped<IPricingService, PricingService>();
// UC: Term & Registration Config
builder.Services.AddScoped<ITermRepository, TermRepository>();
builder.Services.AddScoped<ITermService, TermService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendOnly", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });

    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// 6. Authentication & JWT Bearer
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "FUD-API",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "FUD-Client",
            IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = "user_code"
        };

        // Kiểm tra trạng thái Active + đồng bộ role từ DB (đệm 60s để bảo vệ DB)
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                if (context.Principal?.Identity is System.Security.Claims.ClaimsIdentity identity)
                {
                    var roles = identity.FindAll("role").Concat(identity.FindAll(System.Security.Claims.ClaimTypes.Role)).Select(c => c.Value).Distinct().ToList();
                    foreach (var r in roles)
                    {
                        if (!identity.HasClaim(System.Security.Claims.ClaimTypes.Role, r))
                            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, r));
                        if (!identity.HasClaim("role", r))
                            identity.AddClaim(new System.Security.Claims.Claim("role", r));
                    }
                    var sub = identity.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? identity.FindFirst("sub")?.Value ?? identity.FindFirst("nameid")?.Value;
                    if (sub != null)
                    {
                        if (!identity.HasClaim(System.Security.Claims.ClaimTypes.NameIdentifier, sub))
                            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, sub));
                        if (!identity.HasClaim("sub", sub))
                            identity.AddClaim(new System.Security.Claims.Claim("sub", sub));
                        if (!identity.HasClaim("nameid", sub))
                            identity.AddClaim(new System.Security.Claims.Claim("nameid", sub));
                    }
                }

                var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<FudContext>();
                var userIdStr = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.Principal?.FindFirstValue("sub")
                    ?? context.Principal?.FindFirstValue("nameid");

                if (int.TryParse(userIdStr, out var userId))
                {
                    var cacheKey = $"user_status_{userId}";
                    if (!cache.TryGetValue<string>(cacheKey, out var accountStatus))
                    {
                        var user = await dbContext.Users.AsNoTracking()
                            .Where(u => u.Id == userId && u.DeletedAt == null)
                            .Select(u => new { u.AccountStatus })
                            .FirstOrDefaultAsync();

                        accountStatus = user?.AccountStatus ?? "NotFound";
                        cache.Set(cacheKey, accountStatus, TimeSpan.FromSeconds(60));
                    }

                    if (accountStatus != "Active")
                    {
                        context.Fail("Tài khoản của bạn đã bị khóa hoặc không tồn tại.");
                        return;
                    }

                    // Đồng bộ role hiệu lực từ DB: đảm bảo quyền cấp/thu hồi có hiệu lực thật
                    var rolesCacheKey = $"user_roles_{userId}";
                    if (!cache.TryGetValue<string[]>(rolesCacheKey, out var cachedRoles) || cachedRoles == null)
                    {
                        cachedRoles = (await dbContext.UserRoles.AsNoTracking()
                            .Where(ur => ur.UserId == userId)
                            .Select(ur => ur.Role.RoleName)
                            .ToListAsync()).ToArray();

                        cache.Set(rolesCacheKey, cachedRoles, TimeSpan.FromSeconds(60));
                    }

                    var dbRoles = cachedRoles;
                    var dbIdentity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
                    if (dbIdentity != null)
                    {
                        // Gỡ toàn bộ role claim cũ trong token (cả 2 tên claim: ClaimTypes.Role và "role")
                        foreach (var claim in dbIdentity.FindAll(ClaimTypes.Role)
                                     .Concat(dbIdentity.FindAll("role"))
                                     .ToList())
                            dbIdentity.RemoveClaim(claim);

                        // Gắn lại role theo DB (nguồn sự thật), đồng bộ cả 2 tên claim
                        foreach (var role in dbRoles)
                        {
                            dbIdentity.AddClaim(new Claim(ClaimTypes.Role, role));
                            dbIdentity.AddClaim(new Claim("role", role));
                        }
                    }
                }
            }
        };
    });


// 7. Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireManager", policy => policy.RequireRole("Manager"));
    options.AddPolicy("RequireGuard", policy => policy.RequireRole("Guard"));
    options.AddPolicy("RequireStudent", policy => policy.RequireRole("Student"));
      
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});


// 8. Dependency Injection
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ── Account Management DI (UC: Create Account / Assign Roles / Suspend / Restore) ──
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<INotificationService, GmailNotificationService>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAccountHistoryRepository, AccountHistoryRepository>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddAccountValidators();

// Facilities DI
builder.Services.AddScoped<IBlockRepository, BlockRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IBlockService, BlockService>();
builder.Services.AddScoped<IRoomService, RoomService>();
// UC-Dorm: Dorm Management
builder.Services.AddScoped<FUD.Repositories.Interfaces.Facilities.IDormRepository, FUD.Repositories.Facilities.DormRepository>();
builder.Services.AddScoped<FUD.Services.Interfaces.Facilities.IDormService, FUD.Services.Facilities.DormService>();

// Storage & Cloudinary
builder.Services.Configure<FUD.Settings.pattern.CloudinarySettings>(builder.Configuration.GetSection("Cloudinary"));
builder.Services.AddScoped<FUD.Infrastructure.Storage.IFileStorage, FUD.Infrastructure.Storage.CloudinaryStorage>();

// UC22: Violations DI
builder.Services.AddScoped<FUD.Repositories.Interfaces.Violations.IViolationRepository, FUD.Repositories.Violations.ViolationRepository>();
builder.Services.AddScoped<FUD.Services.Interfaces.Violations.IViolationService, FUD.Services.Violations.ViolationService>();

// ── UC02: Keeping Room DI ──
builder.Services.AddScoped<FUD.Repositories.Interfaces.Keeping.IKeepingRepository, FUD.Repositories.Keeping.KeepingRepository>();
builder.Services.AddScoped<FUD.Services.Interfaces.Keeping.IKeepingEligibilityService, FUD.Services.Keeping.KeepingEligibilityService>();
builder.Services.AddScoped<FUD.Services.Interfaces.Keeping.IKeepingService, FUD.Services.Keeping.KeepingService>();
builder.Services.AddScoped<FUD.Services.Keeping.KeepingPeriodCloseJob>();
// Job đóng kỳ giữ phòng: tự động trả giường về pool booking sau khi kỳ Keeping kết thúc.
builder.Services.AddHostedService<FUD.Services.Keeping.KeepingPeriodCloseHostedService>();

var app = builder.Build();



// Bước 1: Forwarded Headers
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

// app.UseHttpsRedirection();

// Bước 2: Routing
app.UseRouting();

// Bước 3: CORS (Đứng trước RateLimiter để mã 429 vẫn có đủ header CORS)
app.UseCors("FrontendOnly");

// Bước 4: Rate Limiter
app.UseRateLimiter();

// Bước 5: Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-XSS-Protection", "0");
    await next();
});

// Bước 6: Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
