using System.Text;
using FUD.Common;
using FUD.Data;
using FUD.Infrastructure.Auth;
using FUD.Infrastructure.Email;
using FUD.Infrastructure.Notifications;
using FUD.Infrastructure.Security;
using FUD.Middleware;
using FUD.Models;
using FUD.Repositories.Accounts;
using FUD.Repositories.Facilities;
using FUD.Repositories.Interfaces;
using FUD.Repositories.Interfaces.Accounts;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services;
using FUD.Services.Accounts;
using FUD.Services.Facilities;
using FUD.Services.Interfaces;
using FUD.Services.Interfaces.Accounts;
using FUD.Services.Interfaces.Facilities;
using FUD.Settings.pattern;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FUD
{
    public partial class Program
    {
        public static void Main(string[] args)
        {
            // Load biến môi trường từ file .env (DotNetEnv)
            // Format double underscore (__) tự động map: ConnectionStrings__DefaultConnection → ConnectionStrings:DefaultConnection
            DotNetEnv.Env.Load();

            var builder = WebApplication.CreateBuilder(args);

            // ── Cấu hình ───────────────────────────────────────────
            builder.Services.Configure<JwtSettings>(
                builder.Configuration.GetSection("Jwt"));
            builder.Services.Configure<EmailSettings>(
                builder.Configuration.GetSection("Email"));
            builder.Services.Configure<SeedAdminSettings>(
                builder.Configuration.GetSection("SeedAdmin"));

            var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
                             ?? new JwtSettings();


            // ── Database ───────────────────────────────────────────
            builder.Services.AddDbContext<FudContext>(o =>
                o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // ── Facilities: Block & Room ───────────────────────────
            builder.Services.AddScoped<IBlockRepository, BlockRepository>();
            builder.Services.AddScoped<IRoomRepository, RoomRepository>();
            builder.Services.AddScoped<IBlockService, BlockService>();
            builder.Services.AddScoped<IRoomService, RoomService>();

            // ── Accounts: Repository ───────────────────────────────
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IAccountHistoryRepository, AccountHistoryRepository>();
            builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            builder.Services.AddScoped<ITransactionManager, TransactionManager>();

            // ── Accounts: Service ──────────────────────────────────
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<IAuthService, AuthService>();

            // ── Infrastructure adapters ───────────────────────────
            builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
            builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
            builder.Services.AddSingleton<IEmailSender, GmailSmtpSender>();
            builder.Services.AddScoped<INotificationService, GmailNotificationService>();

            // ── Auth ───────────────────────────────────────────────
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

            // Cấu hình JwtBearer bằng IOptions để đọc cấu hình tại runtime
            // (hỗ trợ user-secrets, biến môi trường và override trong test).
            builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtSettings>>((options, jwtAccessor) =>
                {
                    var jwt = jwtAccessor.Value;

                    // Giữ mapping claim mặc định để "role" -> ClaimTypes.Role
                    // và "sub" -> ClaimTypes.NameIdentifier (dùng bởi [Authorize(Roles=...)]).
                    options.MapInboundClaims = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                });

            builder.Services.AddAuthorization();

            // ── CORS ───────────────────────────────────────────────
            // Cho phép SPA React (Vite dev server) gọi API cross-origin.
            // Danh sách origin đọc từ cấu hình "Cors:AllowedOrigins".
            const string CorsPolicy = "FudSpa";
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                                ?? new[] { "https://localhost:5173", "http://localhost:5173" };

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(CorsPolicy, policy => policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders("Content-Disposition"));
            });

            // Add services to the container.
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    // Dùng camelCase cho cả request lẫn response để FE đọc nhất quán.
                    options.JsonSerializerOptions.PropertyNamingPolicy =
                        System.Text.Json.JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.DictionaryKeyPolicy =
                        System.Text.Json.JsonNamingPolicy.CamelCase;
                });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<Program>>();
                try
                {
                    var dbContext = services.GetRequiredService<FudContext>();

                    // Thử kết nối đến cơ sở dữ liệu
                    if (dbContext.Database.CanConnect())
                    {
                        logger.LogInformation("Kết nối Database thành công!");
                    }
                    else
                    {
                        logger.LogWarning("Không thể kết nối đến Database (CanConnect trả về false).");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Lỗi kết nối Database! Vui lòng kiểm tra lại chuỗi kết nối hoặc trạng thái SQL Server.");
                }
            }
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // CORS phải đứng trước GlobalExceptionMiddleware để response lỗi
            // cũng mang header Access-Control-Allow-Origin.
            app.UseCors(CorsPolicy);

            // Global error handling (bọc ngoài cùng)
            app.UseMiddleware<GlobalExceptionMiddleware>();

            app.UseAuthentication();

            // Chặn tài khoản Suspended / token cũ ngay trong pipeline.
            // Đặt TRƯỚC UseAuthorization để tài khoản bị tạm ngưng nhận thông báo
            // rõ ràng thay vì 403 chung chung từ authorization.
            app.UseMiddleware<AccountStatusMiddleware>();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
