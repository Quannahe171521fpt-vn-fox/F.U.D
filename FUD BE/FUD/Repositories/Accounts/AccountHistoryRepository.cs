using FUD.Models;
using FUD.Repositories.Interfaces.Accounts;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories.Accounts
{
    public class AccountHistoryRepository : IAccountHistoryRepository
    {
        private readonly FudContext _context;

        public AccountHistoryRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<AccountStatusHistory> AddStatusHistoryAsync(AccountStatusHistory history)
        {
            _context.AccountStatusHistories.Add(history);
            await _context.SaveChangesAsync();
            return history;
        }

        public async Task<RoleChangeHistory> AddRoleChangeHistoryAsync(RoleChangeHistory history)
        {
            _context.RoleChangeHistories.Add(history);
            await _context.SaveChangesAsync();
            return history;
        }

        public async Task<List<AccountStatusHistory>> GetStatusHistoriesAsync(int userId)
        {
            return await _context.AccountStatusHistories
                .Include(h => h.ChangedBy)
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.ChangedAt)
                .ToListAsync();
        }

        public async Task<List<RoleChangeHistory>> GetRoleHistoriesAsync(int userId)
        {
            return await _context.RoleChangeHistories
                .Include(h => h.ChangedBy)
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.ChangedAt)
                .ToListAsync();
        }

        public async Task UpdateStatusHistoryAsync(AccountStatusHistory history)
        {
            _context.AccountStatusHistories.Update(history);
            await _context.SaveChangesAsync();
        }
    }
}
