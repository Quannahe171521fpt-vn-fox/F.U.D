using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUD.Models;
using FUD.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories;

/// <summary>Repository cho lịch sử trạng thái / vai trò của tài khoản.</summary>
public class AccountHistoryRepository : IAccountHistoryRepository
{
    private readonly FudContext _context;

    public AccountHistoryRepository(FudContext context)
    {
        _context = context;
    }

    public async Task AddStatusHistoryAsync(AccountStatusHistory history)
    {
        await _context.AccountStatusHistories.AddAsync(history);
    }

    public async Task AddRoleChangeHistoryAsync(RoleChangeHistory history)
    {
        await _context.RoleChangeHistories.AddAsync(history);
    }

    public async Task<List<AccountStatusHistory>> GetStatusHistoryAsync(int userId)
    {
        return await _context.AccountStatusHistories
            .AsNoTracking()
            .Include(h => h.ChangedByUser)
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.ChangedAt)
            .ThenByDescending(h => h.Id)
            .ToListAsync();
    }

    public async Task<List<RoleChangeHistory>> GetRoleHistoryAsync(int userId)
    {
        return await _context.RoleChangeHistories
            .AsNoTracking()
            .Include(h => h.ChangedByUser)
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.ChangedAt)
            .ThenByDescending(h => h.Id)
            .ToListAsync();
    }

    public async Task UpdateNotificationStatusAsync(int historyId, string notificationStatus)
    {
        await _context.AccountStatusHistories
            .Where(h => h.Id == historyId)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.NotificationStatus, notificationStatus));
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
