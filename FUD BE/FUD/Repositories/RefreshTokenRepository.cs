using System;
using System.Linq;
using System.Threading.Tasks;
using FUD.Models;
using FUD.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly FudContext _context;

    public RefreshTokenRepository(FudContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
    {
        return await _context.RefreshTokens
            .Include(t => t.User)
                .ThenInclude(u => u.UserRoleUsers)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
    }

    public async Task CreateAsync(RefreshToken token)
    {
        await _context.RefreshTokens.AddAsync(token);
        await _context.SaveChangesAsync();
    }

    public async Task RevokeAsync(RefreshToken token, string revokedByIp, string? replacedByTokenHash = null)
    {
        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = revokedByIp;
        token.ReplacedByTokenHash = replacedByTokenHash;
        await _context.SaveChangesAsync();
    }

    public async Task RevokeAllUserTokensAsync(int userId, string revokedByIp)
    {
        await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, DateTime.UtcNow)
                .SetProperty(t => t.RevokedByIp, revokedByIp));
    }
}
