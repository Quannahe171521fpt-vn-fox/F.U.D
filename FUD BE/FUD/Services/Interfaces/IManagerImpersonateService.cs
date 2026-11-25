using System.Threading.Tasks;
using FUD.DTOs.Auth;

namespace FUD.Services.Interfaces;

/// <summary>
/// Service xử lý nghiệp vụ tra cứu và tạo phiên đăng nhập hộ sinh viên cho Quản lý.
/// </summary>
public interface IManagerImpersonateService
{
    Task<StudentLookupDto> LookupStudentAsync(string query);

    Task<ImpersonationResponseDto> ImpersonateStudentAsync(
        int managerId,
        int studentId,
        bool isCallerImpersonating,
        string? ipAddress,
        string? userAgent);
}

