using FUD.DTOs.Admin.Account;
using FUD.Models;

namespace FUD.Repositories.Interfaces.Accounts
{
    public interface IUserRepository
    {
        Task<(IEnumerable<User> Items, int TotalCount)> GetAllAsync(AccountQueryDto query);

        Task<User?> GetByIdAsync(int id);

        /// <summary>Lấy user kèm vai trò (dùng cho màn hình chi tiết).</summary>
        Task<User?> GetByIdWithRolesAsync(int id);

        Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail);

        Task<bool> IsUsernameExistsAsync(string username, int? excludeId = null);

        Task<bool> IsEmailExistsAsync(string email, int? excludeId = null);

        Task<bool> ExistsAsync(int id);

        Task<List<Role>> GetAllRolesAsync();

        Task<List<Role>> GetRolesByNamesAsync(IEnumerable<string> roleNames);

        Task<List<UserRole>> GetUserRolesAsync(int userId);

        /// <summary>Đếm số tài khoản đang có vai trò Admin (trừ user trong excludeUserId).</summary>
        Task<int> CountAdminsAsync(int? excludeUserId);

        Task<User> CreateAsync(User user);

        Task UpdateAsync(User user);

        Task AddUserRolesAsync(IEnumerable<UserRole> userRoles);

        Task RemoveUserRolesAsync(IEnumerable<UserRole> userRoles);

        Task<bool> SaveChangesAsync();
    }
}
