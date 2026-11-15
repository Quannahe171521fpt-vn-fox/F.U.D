using System.Threading.Tasks;
using FUD.Models;

namespace FUD.Repositories.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);
    Task<RefreshToken?> GetByTokenHashNoTrackingAsync(string tokenHash);
    Task CreateAsync(RefreshToken token);
    Task<bool> TryRotateAsync(RefreshToken existingToken, RefreshToken replacementToken, string revokedByIp);
    Task RevokeAsync(RefreshToken token, string revokedByIp, string? replacedByTokenHash = null);
    Task RevokeAllUserTokensAsync(int userId, string revokedByIp);
}
