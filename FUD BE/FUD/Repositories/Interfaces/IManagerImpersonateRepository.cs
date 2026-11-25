using System.Threading.Tasks;
using FUD.Models;

namespace FUD.Repositories.Interfaces;

/// <summary>
/// Repository truy xuất dữ liệu phục vụ chức năng Đăng nhập hộ sinh viên (Impersonation).
/// </summary>
public interface IManagerImpersonateRepository
{
    Task<User?> FindStudentByEmailOrCodeAsync(string normalizedQuery);
    Task<User?> FindStudentByIdAsync(int studentId);
    Task AddAuditLogAsync(AuditLog auditLog);
}

