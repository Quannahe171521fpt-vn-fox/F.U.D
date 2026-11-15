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

    public async Task<RefreshToken?> GetByTokenHashNoTrackingAsync(string tokenHash)
    {
        return await _context.RefreshTokens
            .AsNoTracking()
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

    public async Task<bool> TryRotateAsync(RefreshToken existingToken, RefreshToken replacementToken, string revokedByIp)
    {
        if (_context.Database.IsRelational())
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            var affected = await _context.RefreshTokens
                .Where(t => t.Id == existingToken.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevokedByIp, revokedByIp)
                    .SetProperty(t => t.ReplacedByTokenHash, replacementToken.TokenHash));

            if (affected == 0)
            {
                await tx.RollbackAsync();
                _context.Entry(existingToken).State = EntityState.Detached;
                return false;
            }

            await _context.RefreshTokens.AddAsync(replacementToken);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }

        var current = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.Id == existingToken.Id);
        if (current == null || current.RevokedAt != null)
        {
            _context.Entry(existingToken).State = EntityState.Detached;
            return false;
        }

        current.RevokedAt = DateTime.UtcNow;
        current.RevokedByIp = revokedByIp;
        current.ReplacedByTokenHash = replacementToken.TokenHash;
        await _context.RefreshTokens.AddAsync(replacementToken);
        await _context.SaveChangesAsync();
        return true;
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
        if (_context.Database.IsRelational())
        {
            await _context.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, DateTime.UtcNow)
                    .SetProperty(t => t.RevokedByIp, revokedByIp));
            return;
        }

        var tokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var t in tokens)
        {
            t.RevokedAt = now;
            t.RevokedByIp = revokedByIp;
        }
        await _context.SaveChangesAsync();
    }
}
