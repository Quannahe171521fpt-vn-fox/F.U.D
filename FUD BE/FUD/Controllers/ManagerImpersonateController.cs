using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FUD.Common;
using FUD.Infrastructure.Auth;
using FUD.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUD.Controllers;

/// <summary>
/// Controller hỗ trợ Quản lý (Manager) tra cứu thông tin và đăng nhập hộ sinh viên.
/// </summary>
[Route("api/manager")]
[ApiController]
[Authorize(Policy = "RequireManager")]
public class ManagerImpersonateController : ControllerBase
{
    private readonly FudContext _db;
    private readonly IJwtTokenGenerator _jwtGen;

    public ManagerImpersonateController(FudContext db, IJwtTokenGenerator jwtGen)
    {
        _db = db;
        _jwtGen = jwtGen;
    }

    /// <summary>
    /// GET /api/manager/lookup-student?q=email_or_mssv
    /// Tra cứu thông tin đầy đủ của sinh viên theo Email hoặc Mã sinh viên (Code).
    /// </summary>
    [HttpGet("lookup-student")]
    public async Task<IActionResult> LookupStudent([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return BadRequest(ApiResponse.Fail("Vui lòng nhập tối thiểu 2 ký tự mã sinh viên hoặc email."));

        var query = q.Trim().ToLowerInvariant();

        var user = await _db.Users
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
                (u.Email.ToLower() == query || u.Code.ToLower() == query) &&
                u.UserRoleUsers.Any(ur => ur.Role.RoleName == "Student"))
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(ApiResponse.Fail("Không tìm thấy sinh viên nào với email hoặc mã số này trong hệ thống."));

        var activeResidency = user.Student?.Residencies?
            .Where(r => r.Status == "Active" || r.Status == "CheckedIn")
            .OrderByDescending(r => r.Id)
            .FirstOrDefault();

        var roles = user.UserRoleUsers.Select(ur => ur.Role.RoleName).ToList();

        var studentData = new
        {
            user.Id,
            user.Code,
            user.FullName,
            user.Email,
            user.Phone,
            user.Gender,
            user.AccountStatus,
            user.SuspensionReason,
            user.CreatedAt,
            PrimaryRole = "Student",
            Roles = roles,
            Major = user.Student?.Major,
            Cohort = user.Student?.Cohort,
            CurrentResidency = activeResidency == null ? null : new
            {
                activeResidency.Id,
                activeResidency.Status,
                TermName = activeResidency.Term?.Name,
                DormName = activeResidency.Bed?.Room?.Block?.Dorm?.Name,
                BlockName = activeResidency.Bed?.Room?.Block?.Name,
                RoomNumber = activeResidency.Bed?.Room?.RoomNumber,
                BedCode = activeResidency.Bed?.BedCode,
                activeResidency.CheckInDate,
                activeResidency.PlannedCheckOutDate,
            }
        };

        return Ok(ApiResponse<object>.Ok(studentData, "Tìm thấy thông tin sinh viên"));
    }

    /// <summary>
    /// POST /api/manager/impersonate/{studentId}
    /// Tạo phiên đăng nhập hộ sinh viên cho Ban Quản trị.
    /// </summary>
    [HttpPost("impersonate/{studentId:int}")]
    public async Task<IActionResult> Impersonate(int studentId)
    {
        var managerIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("nameid");

        if (!int.TryParse(managerIdStr, out var managerId))
            return Unauthorized(ApiResponse.Fail("Token quản lý không hợp lệ"));

        var student = await _db.Users
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
                u.Id == studentId &&
                u.DeletedAt == null &&
                u.UserRoleUsers.Any(ur => ur.Role.RoleName == "Student"))
            .FirstOrDefaultAsync();

        if (student == null)
            return NotFound(ApiResponse.Fail("Sinh viên không tồn tại hoặc không còn hoạt động trên hệ thống."));

        if (student.AccountStatus == "Banned" || student.AccountStatus == "Locked")
        {
            return BadRequest(ApiResponse.Fail($"Tài khoản sinh viên đang ở trạng thái {student.AccountStatus}, không thể đăng nhập hộ."));
        }

        var roles = student.UserRoleUsers.Select(ur => ur.Role.RoleName).ToList();
        var token = _jwtGen.GenerateImpersonationToken(student, roles, managerId);

        var activeResidency = student.Student?.Residencies?
            .Where(r => r.Status == "Active" || r.Status == "CheckedIn")
            .OrderByDescending(r => r.Id)
            .FirstOrDefault();

        var studentInfo = new
        {
            student.Id,
            student.Code,
            student.FullName,
            student.Email,
            student.Gender,
            student.Phone,
            PrimaryRole = "Student",
            Roles = roles,
            student.AccountStatus,
            student.SuspensionReason,
            Major = student.Student?.Major,
            Cohort = student.Student?.Cohort,
            DormId = activeResidency?.Bed?.Room?.Block?.DormId,
            DormName = activeResidency?.Bed?.Room?.Block?.Dorm?.Name,
            CurrentResidency = activeResidency == null ? null : new
            {
                activeResidency.Id,
                activeResidency.Status,
                TermName = activeResidency.Term?.Name,
                DormName = activeResidency.Bed?.Room?.Block?.Dorm?.Name,
                BlockName = activeResidency.Bed?.Room?.Block?.Name,
                RoomNumber = activeResidency.Bed?.Room?.RoomNumber,
                BedCode = activeResidency.Bed?.BedCode
            }
        };

        // Ghi Audit Log vào hệ thống
        try
        {
            var auditLog = new AuditLog
            {
                UserId = managerId,
                ImpersonatedStudentId = student.Id,
                ActionType = "IMPERSONATE_STUDENT",
                TargetEntity = "User",
                TargetId = student.Id.ToString(),
                NewValue = System.Text.Json.JsonSerializer.Serialize(new {
                    ManagerId = managerId,
                    StudentId = student.Id,
                    StudentCode = student.Code,
                    Timestamp = DateTime.UtcNow
                }),
                CreatedAt = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            };
            _db.AuditLogs.Add(auditLog);
            await _db.SaveChangesAsync();
        }
        catch
        {
            // Best effort logging, don't fail impersonation if audit log table has specific constraints
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            impersonationToken = token,
            expiresIn = 7200, // 2 giờ
            student = studentInfo
        }, "Tạo phiên đăng nhập hộ sinh viên thành công"));
    }
}
