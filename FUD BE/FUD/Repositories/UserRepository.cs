using System;
using System.Linq;
using System.Threading.Tasks;
using FUD.Models;
using FUD.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories;

public class UserRepository : IUserRepository
{
    private readonly FudContext _context;

    public UserRepository(FudContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdentifierAsync(string identifier)
    {
        return await _context.Users
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => (u.Code == identifier || u.Email == identifier) && u.DeletedAt == null);
    }

    public async Task<User?> GetByIdAsync(int userId)
    {
        return await _context.Users
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null);
    }

    public async Task<User?> GetByGoogleIdAsync(string googleId)
    {
        return await _context.Users
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.GoogleId == googleId && u.DeletedAt == null);
    }

    public async Task AtomicIncrementFailedLoginAsync(int userId, DateTime? lockoutUntil)
    {
        await _context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.FailedLoginCount, u => u.FailedLoginCount + 1)
                .SetProperty(u => u.LockoutEnd, u => lockoutUntil ?? u.LockoutEnd));
    }

    public async Task AtomicResetFailedLoginAsync(int userId)
    {
        await _context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.FailedLoginCount, 0)
                .SetProperty(u => u.LockoutEnd, (DateTime?)null));
    }

    public async Task UpdateGoogleIdAsync(int userId, string googleId)
    {
        await _context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.GoogleId, googleId));
    }

    public async Task UpdatePasswordAsync(int userId, string newPasswordHash)
    {
        await _context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, newPasswordHash));
    }
}
