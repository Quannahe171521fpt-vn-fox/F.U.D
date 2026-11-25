using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FUD.Common;
using FUD.DTOs.Auth;
using FUD.Infrastructure.Auth;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FUD.Services;

/// <summary>
/// Service xử lý nghiệp vụ tra cứu thông tin và tạo phiên đăng nhập hộ sinh viên (Impersonation).
/// </summary>
public class ManagerImpersonateService : IManagerImpersonateService
{
    private const int ImpersonationTokenLifetimeSeconds = 1800; // 30 phút

    private readonly IManagerImpersonateRepository _repo;
    private readonly IJwtTokenGenerator _jwtGen;
    private readonly ILogger<ManagerImpersonateService>? _logger;

    public ManagerImpersonateService(
        IManagerImpersonateRepository repo,
        IJwtTokenGenerator jwtGen,
        ILogger<ManagerImpersonateService>? logger = null)
    {
        _repo = repo;
        _jwtGen = jwtGen;
        _logger = logger;
    }

    public async Task<StudentLookupDto> LookupStudentAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            throw new ArgumentException("Vui lòng nhập tối thiểu 2 ký tự mã sinh viên hoặc email.");
        }

        var normalizedQuery = query.Trim().ToLowerInvariant();
        var user = await _repo.FindStudentByEmailOrCodeAsync(normalizedQuery)
            ?? throw new KeyNotFoundException("Không tìm thấy sinh viên nào với email hoặc mã số này trong hệ thống.");

        var activeResidency = GetLatestActiveResidency(user);
        var roles = user.UserRoleUsers.Select(ur => ur.Role.RoleName).ToList();

        return new StudentLookupDto
        {
            Id = user.Id,
            Code = user.Code,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Gender = user.Gender,
            AccountStatus = user.AccountStatus,
            SuspensionReason = user.SuspensionReason,
            CreatedAt = user.CreatedAt,
            PrimaryRole = AccountConstants.Roles.Student,
            Roles = roles,
            Major = user.Student?.Major,
            Cohort = user.Student?.Cohort,
            CurrentResidency = MapToResidencyDto(activeResidency, includeDates: true)
        };
    }

    public async Task<ImpersonationResponseDto> ImpersonateStudentAsync(
        int managerId,
        int studentId,
        bool isCallerImpersonating,
        string? ipAddress,
        string? userAgent)
    {
        if (isCallerImpersonating)
        {
            throw new ForbiddenException("Không thể thực hiện đăng nhập hộ từ một phiên đang đăng nhập hộ.");
        }

        var student = await _repo.FindStudentByIdAsync(studentId)
            ?? throw new KeyNotFoundException("Sinh viên không tồn tại hoặc không còn hoạt động trên hệ thống.");

        if (!string.Equals(student.AccountStatus, AccountConstants.Statuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Tài khoản sinh viên đang ở trạng thái {student.AccountStatus}, không thể đăng nhập hộ.");
        }

        var targetRoles = student.UserRoleUsers.Select(ur => ur.Role.RoleName).ToList();
        if (targetRoles.Count != 1 || !targetRoles.Contains(AccountConstants.Roles.Student))
        {
            throw new ForbiddenException("Chỉ được đăng nhập hộ tài khoản có duy nhất vai trò Sinh viên.");
        }

        // Ghi Audit Log vào hệ thống (Fail-closed: phải lưu thành công trước khi cấp token)
        try
        {
            var sanitizedIp = TruncateOrNull(ipAddress, 50);
            var sanitizedUserAgent = TruncateOrNull(userAgent, 255);

            var auditLog = new AuditLog
            {
                UserId = managerId,
                ImpersonatedStudentId = student.Student != null ? student.Id : null,
                ActionType = "IMPERSONATE_STUDENT",
                TargetEntity = "User",
                TargetId = student.Id.ToString(),
                NewValue = JsonSerializer.Serialize(new
                {
                    ManagerId = managerId,
                    StudentId = student.Id,
                    StudentCode = student.Code,
                    Timestamp = DateTime.UtcNow
                }),
                CreatedAt = DateTime.UtcNow,
                IpAddress = sanitizedIp,
                UserAgent = sanitizedUserAgent
            };

            await _repo.AddAuditLogAsync(auditLog);
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Không thể ghi nhận AuditLog khi Manager {ManagerId} đăng nhập hộ sinh viên {StudentId}.",
                managerId,
                student.Id);
            throw new AuditLogWriteException("Không thể ghi nhận nhật ký kiểm toán cho phiên đăng nhập hộ.", ex);
        }

        var roles = new List<string> { AccountConstants.Roles.Student };
        var token = _jwtGen.GenerateImpersonationToken(student, roles, managerId);
        var activeResidency = GetLatestActiveResidency(student);

        return new ImpersonationResponseDto
        {
            ImpersonationToken = token,
            ExpiresIn = ImpersonationTokenLifetimeSeconds,
            Student = new ImpersonatedStudentDto
            {
                Id = student.Id,
                Code = student.Code,
                FullName = student.FullName,
                Email = student.Email,
                Gender = student.Gender,
                Phone = student.Phone,
                PrimaryRole = AccountConstants.Roles.Student,
                Roles = roles,
                AccountStatus = student.AccountStatus,
                SuspensionReason = student.SuspensionReason,
                Major = student.Student?.Major,
                Cohort = student.Student?.Cohort,
                DormId = activeResidency?.Bed?.Room?.Block?.DormId,
                DormName = activeResidency?.Bed?.Room?.Block?.Dorm?.Name,
                CurrentResidency = MapToResidencyDto(activeResidency, includeDates: false)
            }
        };
    }

    private static Residency? GetLatestActiveResidency(User user)
    {
        return user.Student?.Residencies?
            .Where(r => r.Status == "Active" || r.Status == "CheckedIn")
            .OrderByDescending(r => r.Id)
            .FirstOrDefault();
    }

    private static ImpersonationResidencyDto? MapToResidencyDto(Residency? residency, bool includeDates)
    {
        if (residency == null) return null;

        return new ImpersonationResidencyDto
        {
            Id = residency.Id,
            Status = residency.Status,
            TermName = residency.Term?.Name,
            DormName = residency.Bed?.Room?.Block?.Dorm?.Name,
            BlockName = residency.Bed?.Room?.Block?.Name,
            RoomNumber = residency.Bed?.Room?.RoomNumber,
            BedCode = residency.Bed?.BedCode,
            CheckInDate = includeDates ? residency.CheckInDate : null,
            PlannedCheckOutDate = includeDates ? residency.PlannedCheckOutDate : null
        };
    }

    private static string? TruncateOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length > maxLength ? value[..maxLength] : value;
    }
}

/// <summary>
/// Lỗi lưu nhật ký kiểm toán bắt buộc (Fail-closed -> HTTP 500).
/// </summary>
public class AuditLogWriteException : Exception
{
    public AuditLogWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

