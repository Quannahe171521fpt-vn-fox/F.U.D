using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FUD.Models;

namespace FUD.Repositories.Interfaces;

/// <summary>Data Access Layer cho lịch sử tài khoản (trạng thái &amp; vai trò).</summary>
public interface IAccountHistoryRepository
{
    /// <summary>Thêm bản ghi lịch sử trạng thái (chưa lưu).</summary>
    Task AddStatusHistoryAsync(AccountStatusHistory history);

    /// <summary>Thêm bản ghi lịch sử vai trò (chưa lưu).</summary>
    Task AddRoleChangeHistoryAsync(RoleChangeHistory history);

    /// <summary>Lấy lịch sử trạng thái của tài khoản (mới nhất trước).</summary>
    Task<List<AccountStatusHistory>> GetStatusHistoryAsync(int userId);

    /// <summary>Lấy lịch sử vai trò của tài khoản (mới nhất trước).</summary>
    Task<List<RoleChangeHistory>> GetRoleHistoryAsync(int userId);

    /// <summary>Cập nhật trạng thái gửi thông báo của một bản ghi lịch sử.</summary>
    Task UpdateNotificationStatusAsync(int historyId, string notificationStatus);

    /// <summary>Lưu toàn bộ thay đổi.</summary>
    Task SaveChangesAsync();
}
