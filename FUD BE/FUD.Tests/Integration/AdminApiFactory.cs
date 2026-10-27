using System.Net.Http.Headers;
using System.Text.Json;
using FUD.Common;
using FUD.Infrastructure.Auth;
using FUD.Infrastructure.Email;
using FUD.Models;
using FUD.Settings.pattern;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FUD.Tests.Integration
{
    /// <summary>
    /// Factory dùng cho integration test API.
    ///
    /// Dùng SQL Server thật (SQLEXPRESS local) với database riêng cho test vì model
    /// hiện tại dùng SQL Server-specific (default getdate(), computed column, trigger...).
    /// Database được tạo/xóa bằng API của EF Core (Migrate / EnsureDeleted) nên không
    /// cần câu lệnh SQL thủ công.
    ///
    /// IEmailSender được thay bằng bản ghi để assert việc gửi email mà không gửi thật.
    /// </summary>
    public class AdminApiFactory : WebApplicationFactory<Program>
    {
        private const string TestDatabaseName = "FudAccountTest";

        private readonly List<string> _sentEmails = new List<string>();

        private bool _started;

        /// <summary>Đảm bảo host đã khởi động (chạy migration + seed) trước khi dùng dữ liệu seed.</summary>
        public void EnsureStarted()
        {
            if (_started)
                return;

            _started = true;
            CreateClient();
        }

        /// <summary>Id của các user seed (gán sau khi insert vì Id là identity column).</summary>
        public int AdminId { get { EnsureStarted(); return _adminId; } internal set => _adminId = value; }
        public int StudentId { get { EnsureStarted(); return _studentId; } internal set => _studentId = value; }
        public int GuardId { get { EnsureStarted(); return _guardId; } internal set => _guardId = value; }

        private int _adminId;
        private int _studentId;
        private int _guardId;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = GetConnectionString(),
                    ["Jwt:Key"] = "test-signing-key-that-is-long-enough-for-hmac-sha256-algorithm",
                    ["Jwt:Issuer"] = "FUD.Api",
                    ["Jwt:Audience"] = "FUD.Clients",
                    ["Jwt:AccessTokenMinutes"] = "60",
                    ["Jwt:RefreshTokenDays"] = "14",
                    ["Email:SenderEmail"] = "test@fud.local",
                    ["Email:SenderPassword"] = "test"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Email giả: ghi nhận nội dung, không gửi thật
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(new RecordingEmailSender(_sentEmails));

                // Không chạy seed/migration thật ở startup — TestDatabaseInitializer lo việc này
                services.RemoveAll<IHostedService>();
                services.AddSingleton<IHostedService>(sp => new TestDatabaseInitializer(this, sp));
            });
        }

        private static string GetConnectionString()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.Test.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var template = builder["TestDatabase:ConnectionString"]
                ?? "Server=.\\SQLEXPRESS;Database={0};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

            return string.Format(template, TestDatabaseName);
        }

        public void Seed(Action<FudContext> seeder)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FudContext>();
            seeder(context);
        }

        public T GetService<T>() where T : notnull
        {
            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<T>();
        }

        /// <summary>Chẩn đoán cấu hình thực tế đang dùng.</summary>
        public string Diagnose()
        {
            var config = GetService<Microsoft.Extensions.Configuration.IConfiguration>();
            var jwt = GetService<Microsoft.Extensions.Options.IOptions<JwtSettings>>().Value;
            return $"conn={config.GetConnectionString("DefaultConnection")} keylen={jwt.Key.Length} issuer={jwt.Issuer}";
        }

        /// <summary>
        /// Tạo client đã đăng nhập. Roles lấy từ DB (nếu không truyền roles) để token
        /// khớp với quyền thực tế của tài khoản.
        /// </summary>
        public HttpClient CreateClientWithRoles(int userId, params string[] roles)
        {
            var client = CreateClient();
            var generator = GetService<IJwtTokenGenerator>();

            int tokenVersion;
            List<string> effectiveRoles;

            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FudContext>();

                tokenVersion = db.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.TokenVersion)
                    .FirstOrDefault();

                var dbRoles = db.UserRoles
                    .Where(ur => ur.UserId == userId)
                    .Select(ur => ur.Role.RoleName)
                    .ToList();

                effectiveRoles = roles.Length > 0 ? roles.ToList() : dbRoles;
            }

            var (token, _) = generator.GenerateAccessToken(userId, $"user{userId}", effectiveRoles, tokenVersion);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        /// <summary>
        /// Gỡ vai trò Admin khỏi mọi tài khoản trừ tài khoản được giữ lại.
        /// Dùng để dựng kịch bản "Admin cuối cùng" trong test.
        /// </summary>
        public void KeepOnlyAdmin(int keepUserId)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FudContext>();

            var adminRole = db.Roles.FirstOrDefault(r => r.RoleName == AccountConstants.RoleAdmin);
            if (adminRole == null)
                return;

            var extra = db.UserRoles
                .Where(ur => ur.RoleId == adminRole.Id && ur.UserId != keepUserId)
                .ToList();

            db.UserRoles.RemoveRange(extra);
            db.SaveChanges();
        }

        /// <summary>Tạm thời cấp lại vai trò Admin cho một tài khoản (dùng để dựng kịch bản test).</summary>
        public void GrantAdminRole(int userId)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FudContext>();

            var adminRole = db.Roles.FirstOrDefault(r => r.RoleName == AccountConstants.RoleAdmin);
            if (adminRole == null)
                return;

            var exists = db.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == adminRole.Id);
            if (exists)
                return;

            db.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = adminRole.Id,
                AssignedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        /// <summary>Tạo một tài khoản mới (Active) trực tiếp trong DB và trả về id.</summary>
        public int SeedUser(string username, string email, string fullName, params string[] roles)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FudContext>();
            var now = DateTime.UtcNow;

            var user = new User
            {
                Code = $"SEED-{username}",
                Username = username,
                FullName = fullName,
                Email = email,
                Gender = "Other",
                PasswordHash = "hashed:Pass1234",
                AccountStatus = AccountConstants.StatusActive,
                CreatedAt = now,
                UpdatedAt = now,
                TokenVersion = 0
            };

            db.Users.Add(user);
            db.SaveChanges();

            foreach (var roleName in roles)
            {
                var role = db.Roles.FirstOrDefault(r => r.RoleName == roleName);
                if (role == null)
                {
                    role = new Role { RoleName = roleName };
                    db.Roles.Add(role);
                    db.SaveChanges();
                }

                db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id,
                    AssignedAt = now
                });
            }

            db.SaveChanges();
            return user.Id;
        }

        public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        {
            var text = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(text).RootElement.Clone();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
                return;

            try
            {
                using var scope = Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FudContext>();
                db.Database.EnsureDeleted();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Không xóa được database test: {ex.Message}");
            }
        }

        private sealed class RecordingEmailSender : IEmailSender
        {
            private readonly List<string> _sent;

            public RecordingEmailSender(List<string> sent)
            {
                _sent = sent;
            }

            public Task<bool> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
            {
                _sent.Add($"{toEmail}|{subject}");
                return Task.FromResult(true);
            }
        }

        private sealed class TestDatabaseInitializer : IHostedService
        {
            private readonly AdminApiFactory _factory;
            private readonly IServiceProvider _services;

            public TestDatabaseInitializer(AdminApiFactory factory, IServiceProvider services)
            {
                _factory = factory;
                _services = services;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FudContext>();

                // Xóa database cũ (nếu có) rồi tạo lại từ migration của dự án.
                await db.Database.EnsureDeletedAsync(cancellationToken);
                await db.Database.MigrateAsync(cancellationToken);

                await TestDataSeeder.SeedAsync(db, _factory);
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }

    public static class TestDataSeeder
    {
        public static async Task SeedAsync(FudContext db, AdminApiFactory factory)
        {
            var now = DateTime.UtcNow;

            // Users.Id là identity column — không set giá trị tường minh.
            var admin = new User
            {
                Code = "ADMIN001",
                Username = "admin",
                FullName = "Quản trị hệ thống",
                Email = "admin@fud.local",
                Gender = "Other",
                PasswordHash = "hashed:Pass1234",
                AccountStatus = AccountConstants.StatusActive,
                CreatedAt = now,
                UpdatedAt = now,
                TokenVersion = 0
            };

            var student = new User
            {
                Code = "STU001",
                Username = "student01",
                FullName = "Nguyễn Văn A",
                Email = "a@fud.local",
                Gender = "Other",
                PasswordHash = "hashed:Pass1234",
                AccountStatus = AccountConstants.StatusActive,
                CreatedAt = now,
                UpdatedAt = now,
                TokenVersion = 0
            };

            var guard = new User
            {
                Code = "GRD001",
                Username = "guard01",
                FullName = "Trần Văn B",
                Email = "b@fud.local",
                Gender = "Other",
                PasswordHash = "hashed:Pass1234",
                AccountStatus = AccountConstants.StatusActive,
                CreatedAt = now,
                UpdatedAt = now,
                TokenVersion = 0
            };

            db.Users.AddRange(admin, student, guard);
            await db.SaveChangesAsync();

            // Vai trò: ưu tiên dùng role đã seed sẵn, nếu chưa có thì tạo mới.
            async Task<Role> EnsureRoleAsync(string name, string description)
            {
                var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == name);
                if (role == null)
                {
                    role = new Role { RoleName = name, Description = description };
                    db.Roles.Add(role);
                    await db.SaveChangesAsync();
                }
                return role;
            }

            var roleStudent = await EnsureRoleAsync(AccountConstants.RoleStudent, "Sinh viên");
            var roleGuard = await EnsureRoleAsync(AccountConstants.RoleGuard, "Bảo vệ");
            var roleAdmin = await EnsureRoleAsync(AccountConstants.RoleAdmin, "Admin");

            db.UserRoles.AddRange(
                new UserRole { UserId = admin.Id, RoleId = roleAdmin.Id, AssignedAt = now },
                new UserRole { UserId = student.Id, RoleId = roleStudent.Id, AssignedAt = now },
                new UserRole { UserId = guard.Id, RoleId = roleGuard.Id, AssignedAt = now });

            await db.SaveChangesAsync();

            factory.AdminId = admin.Id;
            factory.StudentId = student.Id;
            factory.GuardId = guard.Id;
        }
    }
}
