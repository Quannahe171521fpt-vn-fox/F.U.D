using FUD.Models;
using FUD.Repositories.Interfaces.Accounts;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories.Accounts
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly FudContext _context;

        public RefreshTokenRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<RefreshToken> CreateAsync(RefreshToken token)
        {
            _context.RefreshTokens.Add(token);
            await _context.SaveChangesAsync();
            return token;
        }

        public async Task<RefreshToken?> GetByHashAsync(string tokenHash)
        {
            return await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        }

        public async Task<int> RevokeAllForUserAsync(int userId, int? revokedById = null)
        {
            var tokens = await _context.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ToListAsync();

            if (tokens.Count == 0)
                return 0;

            var now = DateTime.UtcNow;
            foreach (var t in tokens)
            {
                t.RevokedAt = now;
                t.RevokedById = revokedById;
            }

            await _context.SaveChangesAsync();
            return tokens.Count;
        }

        public async Task<int> RevokeAsync(string tokenHash, int? revokedById = null)
        {
            var token = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
            if (token == null || token.RevokedAt != null)
                return 0;

            token.RevokedAt = DateTime.UtcNow;
            token.RevokedById = revokedById;
            await _context.SaveChangesAsync();
            return 1;
        }

        public async Task<bool> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
