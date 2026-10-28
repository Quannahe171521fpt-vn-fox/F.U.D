using System.Text;
using DotNetEnv;
using FUD.Models;
using FUD.Services;
using FUD.Services.Interfaces;
using FUD.Settings.pattern;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using FUD.Repositories.Facilities;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Facilities;
using FUD.Services.Interfaces.Facilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace FUD
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Load file .env nếu có
            Env.TraversePath().Load();

            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddEnvironmentVariables();

            // Database Context
            builder.Services.AddDbContext<FudContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Cấu hình Settings
            builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

            // Đăng ký Services
            builder.Services.AddScoped<IAuthService, AuthService>();

            // Cấu hình JWT Authentication
            var jwtKey = builder.Configuration["Jwt:Key"] ?? "chuoi-bi-mat-fpt-university-dormitory-auth-key-2026-super-secret";
            var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "FUD-API";
            var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "FUD-Client";

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ClockSkew = TimeSpan.Zero
                };
            });

            // Cấu hình Phân quyền (Authorization Policies)
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
                options.AddPolicy("RequireManager", policy => policy.RequireRole("Manager", "Admin"));
                options.AddPolicy("RequireGuard", policy => policy.RequireRole("Guard", "Manager", "Admin"));
                options.AddPolicy("RequireStudent", policy => policy.RequireRole("Student"));
            });

            // Cấu hình CORS cho Frontend React
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowReactApp", policy =>
                {
                    policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });

            // ── Facilities: Block & Room ──────────────────────────────
            builder.Services.AddScoped<IBlockRepository, BlockRepository>();
            builder.Services.AddScoped<IRoomRepository, RoomRepository>();
            builder.Services.AddScoped<IBlockService, BlockService>();
            builder.Services.AddScoped<IRoomService, RoomService>();

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();

            // Swagger hỗ trợ nhập Bearer Token
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "FUD Dormitory Management API",
                    Version = "v1",
                    Description = "API hệ thống Quản lý Ký túc xá Đại học FPT (F.U.D) - Hỗ trợ phân quyền RBAC & lọc giới tính."
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Nhập token JWT theo định dạng: Bearer {token}",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecuritySchemeReference("Bearer", null, null),
                        new List<string>()
                    }
                });
            });

            var app = builder.Build();
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<Program>>();
                try
                {
                    var dbContext = services.GetRequiredService<FudContext>();

            // HTTP request pipeline
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
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FUD API v1");
                });
            }

            app.UseHttpsRedirection();

            app.UseCors("AllowReactApp");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}