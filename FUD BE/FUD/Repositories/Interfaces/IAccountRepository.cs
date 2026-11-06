using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FUD.DTOs.Account;
using FUD.Models;

namespace FUD.Repositories.Interfaces;

/// <summary>Data Access Layer cho module Account Management.</summary>
public interface IAccountRepository
{
    /// <summary>Lấy tài khoản theo Id kèm vai trò (không theo dõi thay đổi).</summary>
    Task<User?> GetByIdAsync(int userId);

    /// <summary>Lấy tài khoản theo Id kèm vai trò, dùng cho các thao tác ghi (theo dõi thay đổi).</summary>
    Task<User?> GetTrackedByIdAsync(int userId);

    /// <summary>Phân trang + lọc danh sách tài khoản.</summary>
    Task<(IEnumerable<User> Items, int TotalCount)> GetAllAsync(AccountQueryDto query);

    /// <summary>Kiểm tra Username hoặc Email đã tồn tại hay chưa.</summary>
    Task<bool> ExistsByUsernameOrEmailAsync(string username, string email, int? excludeUserId = null);

    /// <summary>Lấy danh sách Role hợp lệ theo tên (chỉ trả về role có thật trong DB).</summary>
    Task<List<Role>> GetRolesByNamesAsync(IEnumerable<string> roleNames);

    /// <summary>Lấy danh sách tên vai trò hiện tại của tài khoản.</summary>
    Task<List<string>> GetUserRoleNamesAsync(int userId);

    /// <summary>Đếm số tài khoản đang hoạt động và có vai trò Admin.</summary>
    Task<int> CountActiveAdminsAsync(int? excludeUserId = null);

    /// <summary>Thêm tài khoản mới (chưa lưu).</summary>
    Task<User> AddAsync(User user);

    /// <summary>Thêm các bản ghi User_Roles mới (chưa lưu).</summary>
    Task AddUserRolesAsync(int userId, IEnumerable<Role> roles, int? assignedById);

    /// <summary>Xóa các bản ghi User_Roles không còn nằm trong danh sách giữ lại.</summary>
    Task RemoveUserRolesAsync(int userId, IEnumerable<int> keepRoleIds);

    /// <summary>Lưu toàn bộ thay đổi.</summary>
    Task SaveChangesAsync();

    /// <summary>Mở giao dịch DB cho các thao tác ghi nhiều bảng.</summary>
    Task<IAccountTransaction> BeginTransactionAsync();
}
