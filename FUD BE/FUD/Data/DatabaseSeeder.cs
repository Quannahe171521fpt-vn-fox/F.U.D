using FUD.Common;
using FUD.Infrastructure.Security;
using FUD.Models;
using FUD.Settings.pattern;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FUD.Data
{
    /// <summary>
    /// Seed tài khoản Admin mặc định (và vai trò hệ thống nếu chưa có).
    /// Thông tin đọc từ cấu hình: SeedAdmin:Username / FullName / Email / Password.
    /// Không hard-code mật khẩu trong source code.
    /// </summary>
    public static class DatabaseSeeder
    {
        public static async Task SeedAdminAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var provider = scope.ServiceProvider;

            var db = provider.GetRequiredService<FudContext>();
            var settings = provider.GetRequiredService<IOptions<SeedAdminSettings>>().Value;
            var passwordHasher = provider.GetRequiredService<IPasswordHasher>();

            await EnsureRolesAsync(db);

            if (string.IsNullOrWhiteSpace(settings.Username)
                || string.IsNullOrWhiteSpace(settings.Email)
                || string.IsNullOrWhiteSpace(settings.Password))
                return;

            var username = settings.Username.Trim();
            var email = settings.Email.Trim();

            var adminExists = await db.Users.AnyAsync(u => u.Username == username);
            if (adminExists)
                return;

            var now = DateTime.UtcNow;
            var admin = new User
            {
                Code = string.IsNullOrWhiteSpace(settings.Code) ? username : settings.Code.Trim(),
                Username = username,
                FullName = string.IsNullOrWhiteSpace(settings.FullName) ? "Quản trị hệ thống" : settings.FullName.Trim(),
                Email = email,
                Gender = "Other",
                PasswordHash = passwordHasher.Hash(settings.Password),
                AccountStatus = AccountConstants.StatusActive,
                CreatedAt = now,
                UpdatedAt = now,
                TokenVersion = 0
            };

            db.Users.Add(admin);
            await db.SaveChangesAsync();

            var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == AccountConstants.RoleAdmin);
            if (adminRole != null)
            {
                db.UserRoles.Add(new UserRole
                {
                    UserId = admin.Id,
                    RoleId = adminRole.Id,
                    AssignedAt = now
                });
                await db.SaveChangesAsync();
            }

            db.Admins.Add(new Admin { UserId = admin.Id });
            await db.SaveChangesAsync();
        }

        /// <summary>Đảm bảo 4 vai trò hệ thống tồn tại (id cố định 1..4).</summary>
        public static async Task EnsureRolesAsync(FudContext db)
        {
            var roles = new[]
            {
                new Role { Id = 1, RoleName = AccountConstants.RoleStudent, Description = "Sinh viên" },
                new Role { Id = 2, RoleName = AccountConstants.RoleManager, Description = "Quản lý ký túc xá" },
                new Role { Id = 3, RoleName = AccountConstants.RoleGuard, Description = "Bảo vệ / nhân viên trực" },
                new Role { Id = 4, RoleName = AccountConstants.RoleAdmin, Description = "Quản trị hệ thống" }
            };

            foreach (var role in roles)
            {
                var exists = await db.Roles.AnyAsync(r => r.Id == role.Id || r.RoleName == role.RoleName);
                if (!exists)
                    db.Roles.Add(role);
            }

            await db.SaveChangesAsync();
        }
    }
}
