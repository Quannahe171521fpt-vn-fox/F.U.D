﻿using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using FUD.Common;
using FUD.Infrastructure.Auth;
using FUD.Models;
using FUD.Repositories;
using FUD.Repositories.Facilities;
using FUD.Repositories.Interfaces;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services;
using FUD.Services.Facilities;
using FUD.Services.Interfaces;
using FUD.Services.Interfaces.Facilities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Load biến môi trường DotNetEnv
DotNetEnv.Env.TraversePath().Load();

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

// 5. CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                     ?? new[] { "http://localhost:5173" };


// ── Facilities: UC28 Equipment & UC29 News ─────────────────
builder.Services.AddScoped<IEquipmentRepository, EquipmentRepository>();
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<INewsRepository, NewsRepository>();
builder.Services.AddScoped<INewsService, NewsService>();
// UC12: Pricing Settings
builder.Services.AddScoped<IPricingRepository, PricingRepository>();
builder.Services.AddScoped<IPricingService, PricingService>();

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

        // Kiểm tra trạng thái Active qua MemoryCache (Đệm 60s để bảo vệ DB)
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<FudContext>();
                var userIdStr = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

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

// Facilities DI
builder.Services.AddScoped<IBlockRepository, BlockRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IBlockService, BlockService>();
builder.Services.AddScoped<IRoomService, RoomService>();

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- PIPELINE MIDDLEWARE CHUẨN XÁC ---

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

app.UseHttpsRedirection();

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
