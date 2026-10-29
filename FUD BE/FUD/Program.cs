using FUD.Models;
using FUD.Repositories.Facilities;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Facilities;
using FUD.Services.Interfaces.Facilities;
using Microsoft.EntityFrameworkCore;

namespace FUD
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Load biến môi trường từ file .env (DotNetEnv)
            // Format double underscore (__) tự động map: ConnectionStrings__DefaultConnection → ConnectionStrings:DefaultConnection
            DotNetEnv.Env.Load();

            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddDbContext<FudContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // ── Facilities: Block & Room ──────────────────────────────
            builder.Services.AddScoped<IBlockRepository, BlockRepository>();
            builder.Services.AddScoped<IRoomRepository, RoomRepository>();
            builder.Services.AddScoped<IBlockService, BlockService>();
            builder.Services.AddScoped<IRoomService, RoomService>();

            // ── Facilities: UC28 Equipment & UC29 News ─────────────────
            builder.Services.AddScoped<IEquipmentRepository, EquipmentRepository>();
            builder.Services.AddScoped<IEquipmentService, EquipmentService>();
            builder.Services.AddScoped<INewsRepository, NewsRepository>();
            builder.Services.AddScoped<INewsService, NewsService>();

            // CORS policy for Frontend Vite
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            // Add services to the container.
            builder.Services.AddControllers();
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

            app.UseCors("AllowAll");

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
