using System;
using System.Threading.Tasks;
using FUD.Models;

namespace FUD.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdentifierAsync(string identifier);
    Task<User?> GetByIdAsync(int userId);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByGoogleIdAsync(string googleId);
    Task AtomicIncrementFailedLoginAsync(int userId, DateTime? lockoutUntil);
    Task AtomicResetFailedLoginAsync(int userId);
    Task UpdateGoogleIdAsync(int userId, string googleId);
    Task UpdatePasswordAsync(int userId, string newPasswordHash);
}
