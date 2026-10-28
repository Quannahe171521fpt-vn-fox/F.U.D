using FUD.Common;
using FUD.DTOs.Admin.Account;
using FUD.Models;
using FUD.Repositories.Interfaces.Accounts;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories.Accounts
{
    public class UserRepository : IUserRepository
    {
        private readonly FudContext _context;

        public UserRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<(IEnumerable<User> Items, int TotalCount)> GetAllAsync(AccountQueryDto query)
        {
            var q = _context.Users
                .Include(u => u.UserRoleUsers)
                    .ThenInclude(ur => ur.Role)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim();
                q = q.Where(u => u.Username.Contains(kw)
                             || u.FullName.Contains(kw)
                             || u.Email.Contains(kw));
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
                q = q.Where(u => u.AccountStatus == query.Status);

            if (!string.IsNullOrWhiteSpace(query.RoleName))
                q = q.Where(u => u.UserRoleUsers.Any(ur => ur.Role.RoleName == query.RoleName));

            var totalCount = await q.CountAsync();

            var items = await q
                .OrderBy(u => u.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User?> GetByIdWithRolesAsync(int id)
        {
            return await _context.Users
                .Include(u => u.UserRoleUsers)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.SuspendedBy)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail)
        {
            var value = usernameOrEmail.Trim();
            return await _context.Users
                .Include(u => u.UserRoleUsers)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Username == value || u.Email == value);
        }

        public async Task<bool> IsUsernameExistsAsync(string username, int? excludeId = null)
        {
            return await _context.Users
                .AnyAsync(u => u.Username == username && (excludeId == null || u.Id != excludeId));
        }

        public async Task<bool> IsEmailExistsAsync(string email, int? excludeId = null)
        {
            return await _context.Users
                .AnyAsync(u => u.Email == email && (excludeId == null || u.Id != excludeId));
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Users.AnyAsync(u => u.Id == id);
        }

        public async Task<List<Role>> GetAllRolesAsync()
        {
            return await _context.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        }

        public async Task<List<Role>> GetRolesByNamesAsync(IEnumerable<string> roleNames)
        {
            var names = roleNames.Select(r => r.Trim()).Distinct().ToList();
            return await _context.Roles
                .Where(r => names.Contains(r.RoleName))
                .ToListAsync();
        }

        public async Task<List<UserRole>> GetUserRolesAsync(int userId)
        {
            return await _context.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == userId)
                .ToListAsync();
        }

        public async Task<int> CountAdminsAsync(int? excludeUserId)
        {
            return await _context.UserRoles
                .Where(ur => ur.Role.RoleName == AccountConstants.RoleAdmin)
                .Where(ur => excludeUserId == null || ur.UserId != excludeUserId)
                .Select(ur => ur.UserId)
                .Distinct()
                .CountAsync();
        }

        public async Task<User> CreateAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task AddUserRolesAsync(IEnumerable<UserRole> userRoles)
        {
            await _context.UserRoles.AddRangeAsync(userRoles);
        }

        public async Task RemoveUserRolesAsync(IEnumerable<UserRole> userRoles)
        {
            _context.UserRoles.RemoveRange(userRoles);
        }

        public async Task<bool> SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
