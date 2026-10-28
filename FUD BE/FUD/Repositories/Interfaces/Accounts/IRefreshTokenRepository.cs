using FUD.Models;

namespace FUD.Repositories.Interfaces.Accounts
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken> CreateAsync(RefreshToken token);

        Task<RefreshToken?> GetByHashAsync(string tokenHash);

        /// <summary>Thu hồi toàn bộ refresh token còn hiệu lực của user.</summary>
        Task<int> RevokeAllForUserAsync(int userId, int? revokedById = null);

        Task<int> RevokeAsync(string tokenHash, int? revokedById = null);

        Task<bool> SaveChangesAsync();
    }
}
