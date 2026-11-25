using System.Linq;
using System.Threading.Tasks;
using FUD.Models;
using FUD.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories;

/// <summary>
/// Repository triển khai truy vấn sinh viên và lưu AuditLog cho chức năng Đăng nhập hộ sinh viên.
/// </summary>
public class ManagerImpersonateRepository : IManagerImpersonateRepository
{
    private readonly FudContext _context;

    public ManagerImpersonateRepository(FudContext context)
    {
        _context = context;
    }

    public async Task<User?> FindStudentByEmailOrCodeAsync(string normalizedQuery)
    {
        return await BuildStudentBaseQuery()
            .Where(u => u.Email.ToLower() == normalizedQuery || u.Code.ToLower() == normalizedQuery)
            .FirstOrDefaultAsync();
    }

    public async Task<User?> FindStudentByIdAsync(int studentId)
    {
        return await BuildStudentBaseQuery()
            .Where(u => u.Id == studentId)
            .FirstOrDefaultAsync();
    }

    public async Task AddAuditLogAsync(AuditLog auditLog)
    {
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }

    private IQueryable<User> BuildStudentBaseQuery()
    {
        return _context.Users
            .Include(u => u.UserRoleUsers).ThenInclude(ur => ur.Role)
            .Include(u => u.Student)
                .ThenInclude(s => s!.Residencies)
                    .ThenInclude(r => r.Bed)
                        .ThenInclude(b => b.Room)
                            .ThenInclude(rm => rm.Block)
                                .ThenInclude(blk => blk.Dorm)
            .Include(u => u.Student)
                .ThenInclude(s => s!.Residencies)
                    .ThenInclude(r => r.Term)
            .Where(u =>
                u.DeletedAt == null &&
                u.UserRoleUsers.Any(ur => ur.Role.RoleName == AccountConstants.Roles.Student));
    }
}

