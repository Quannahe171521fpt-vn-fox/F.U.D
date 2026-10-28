using FUD.Models;

namespace FUD.Repositories.Interfaces.Accounts
{
    public interface IAccountHistoryRepository
    {
        Task<AccountStatusHistory> AddStatusHistoryAsync(AccountStatusHistory history);

        Task<RoleChangeHistory> AddRoleChangeHistoryAsync(RoleChangeHistory history);

        Task<List<AccountStatusHistory>> GetStatusHistoriesAsync(int userId);

        Task<List<RoleChangeHistory>> GetRoleHistoriesAsync(int userId);

        Task UpdateStatusHistoryAsync(AccountStatusHistory history);
    }
}
