using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUD.DTOs.Account;
using FUD.Models;
using FUD.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories;

/// <summary>
/// Repository truy vấn nhật ký kiểm toán hợp nhất từ Audit_Logs, Role_Change_History và Account_Status_History.
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly FudContext _context;

    public AuditLogRepository(FudContext context)
    {
        _context = context;
    }

    public async Task<(IEnumerable<AuditLogItemDto> Items, int TotalCount)> GetUnifiedLogsAsync(AuditLogQueryDto query)
    {
        var auditLogsQuery = _context.AuditLogs
            .AsNoTracking()
            .Select(x => new AuditLogItemDto
            {
                Id = x.Id,
                LogType = x.ActionType == "IMPERSONATE_STUDENT" ? "Impersonation" : "System",
                ActorId = x.UserId,
                ActorName = x.User != null ? x.User.FullName : null,
                ActorCode = x.User != null ? x.User.Code : null,
                TargetUserId = x.ImpersonatedStudentId,
                TargetUserName = x.ImpersonatedStudent != null && x.ImpersonatedStudent.User != null ? x.ImpersonatedStudent.User.FullName : null,
                TargetUserCode = x.ImpersonatedStudent != null && x.ImpersonatedStudent.User != null ? x.ImpersonatedStudent.User.Code : null,
                ActionType = x.ActionType,
                TargetEntity = x.TargetEntity,
                TargetId = x.TargetId,
                OldValue = x.OldValue,
                NewValue = x.NewValue,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
                CreatedAt = x.CreatedAt,
                Notes = null
            });

        var roleHistoriesQuery = _context.RoleChangeHistories
            .AsNoTracking()
            .Select(x => new AuditLogItemDto
            {
                Id = x.Id,
                LogType = "RoleChange",
                ActorId = (int?)x.ChangedBy,
                ActorName = x.ChangedByUser != null ? x.ChangedByUser.FullName : null,
                ActorCode = x.ChangedByUser != null ? x.ChangedByUser.Code : null,
                TargetUserId = (int?)x.UserId,
                TargetUserName = x.User != null ? x.User.FullName : null,
                TargetUserCode = x.User != null ? x.User.Code : null,
                ActionType = x.Action,
                TargetEntity = "Role",
                TargetId = x.UserId.ToString(),
                OldValue = x.OldRoles,
                NewValue = x.NewRoles,
                IpAddress = null,
                UserAgent = null,
                CreatedAt = x.ChangedAt,
                Notes = null
            });

        var statusHistoriesQuery = _context.AccountStatusHistories
            .AsNoTracking()
            .Select(x => new AuditLogItemDto
            {
                Id = x.Id,
                LogType = "AccountStatus",
                ActorId = (int?)x.ChangedBy,
                ActorName = x.ChangedByUser != null ? x.ChangedByUser.FullName : null,
                ActorCode = x.ChangedByUser != null ? x.ChangedByUser.Code : null,
                TargetUserId = (int?)x.UserId,
                TargetUserName = x.User != null ? x.User.FullName : null,
                TargetUserCode = x.User != null ? x.User.Code : null,
                ActionType = x.NewStatus,
                TargetEntity = "AccountStatus",
                TargetId = x.UserId.ToString(),
                OldValue = x.OldStatus,
                NewValue = x.NewStatus,
                IpAddress = null,
                UserAgent = null,
                CreatedAt = x.ChangedAt,
                Notes = string.IsNullOrEmpty(x.Reason) ? x.Notes : (string.IsNullOrEmpty(x.Notes) ? x.Reason : (x.Reason + " - " + x.Notes))
            });

        IQueryable<AuditLogItemDto> combined;

        var filterType = query.LogType?.Trim();
        if (string.Equals(filterType, "Impersonation", StringComparison.OrdinalIgnoreCase))
        {
            combined = auditLogsQuery.Where(x => x.LogType == "Impersonation");
        }
        else if (string.Equals(filterType, "RoleChange", StringComparison.OrdinalIgnoreCase))
        {
            combined = roleHistoriesQuery;
        }
        else if (string.Equals(filterType, "AccountStatus", StringComparison.OrdinalIgnoreCase))
        {
            combined = statusHistoriesQuery;
        }
        else if (string.Equals(filterType, "System", StringComparison.OrdinalIgnoreCase))
        {
            combined = auditLogsQuery.Where(x => x.LogType == "System");
        }
        else
        {
            combined = auditLogsQuery.Concat(roleHistoriesQuery).Concat(statusHistoriesQuery);
        }

        if (query.ActorId.HasValue)
        {
            combined = combined.Where(x => x.ActorId == query.ActorId.Value);
        }

        if (query.FromDate.HasValue)
        {
            combined = combined.Where(x => x.CreatedAt >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            var toDate = query.ToDate.Value;
            if (toDate.TimeOfDay == TimeSpan.Zero)
            {
                toDate = toDate.Date.AddDays(1).AddTicks(-1);
            }
            combined = combined.Where(x => x.CreatedAt <= toDate);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var kw = query.Search.Trim();
            combined = combined.Where(x =>
                (x.ActorName != null && x.ActorName.Contains(kw)) ||
                (x.ActorCode != null && x.ActorCode.Contains(kw)) ||
                (x.TargetUserName != null && x.TargetUserName.Contains(kw)) ||
                (x.TargetUserCode != null && x.TargetUserCode.Contains(kw)) ||
                (x.ActionType != null && x.ActionType.Contains(kw)) ||
                (x.TargetEntity != null && x.TargetEntity.Contains(kw)) ||
                (x.Notes != null && x.Notes.Contains(kw)) ||
                (x.NewValue != null && x.NewValue.Contains(kw))
            );
        }

        var totalCount = await combined.CountAsync();
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);

        var items = await combined
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<AuditLogDetailDto?> GetDetailAsync(int id, string? logType)
    {
        var type = logType?.Trim();

        if (string.Equals(type, "RoleChange", StringComparison.OrdinalIgnoreCase))
        {
            return await GetRoleChangeDetailAsync(id);
        }

        if (string.Equals(type, "AccountStatus", StringComparison.OrdinalIgnoreCase))
        {
            return await GetAccountStatusDetailAsync(id);
        }

        if (string.Equals(type, "Impersonation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "System", StringComparison.OrdinalIgnoreCase))
        {
            return await GetAuditLogDetailAsync(id);
        }

        // Tự động tìm kiếm theo thứ tự nếu logType không truyền
        var audit = await GetAuditLogDetailAsync(id);
        if (audit != null) return audit;

        var role = await GetRoleChangeDetailAsync(id);
        if (role != null) return role;

        return await GetAccountStatusDetailAsync(id);
    }

    private async Task<AuditLogDetailDto?> GetAuditLogDetailAsync(int id)
    {
        var x = await _context.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.ImpersonatedStudent)
                .ThenInclude(s => s!.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (x == null) return null;

        return new AuditLogDetailDto
        {
            Id = x.Id,
            LogType = x.ActionType == "IMPERSONATE_STUDENT" ? "Impersonation" : "System",
            ActorId = x.UserId,
            ActorName = x.User?.FullName,
            ActorCode = x.User?.Code,
            TargetUserId = x.ImpersonatedStudentId,
            TargetUserName = x.ImpersonatedStudent?.User?.FullName,
            TargetUserCode = x.ImpersonatedStudent?.User?.Code,
            ActionType = x.ActionType,
            TargetEntity = x.TargetEntity,
            TargetId = x.TargetId,
            OldValue = x.OldValue,
            NewValue = x.NewValue,
            RawPayload = x.NewValue,
            IpAddress = x.IpAddress,
            UserAgent = x.UserAgent,
            CreatedAt = x.CreatedAt,
            Notes = null
        };
    }

    private async Task<AuditLogDetailDto?> GetRoleChangeDetailAsync(int id)
    {
        var x = await _context.RoleChangeHistories
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.ChangedByUser)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (x == null) return null;

        return new AuditLogDetailDto
        {
            Id = x.Id,
            LogType = "RoleChange",
            ActorId = x.ChangedBy,
            ActorName = x.ChangedByUser?.FullName,
            ActorCode = x.ChangedByUser?.Code,
            TargetUserId = x.UserId,
            TargetUserName = x.User?.FullName,
            TargetUserCode = x.User?.Code,
            ActionType = x.Action,
            TargetEntity = "Role",
            TargetId = x.UserId.ToString(),
            OldValue = x.OldRoles,
            NewValue = x.NewRoles,
            RawPayload = null,
            IpAddress = null,
            UserAgent = null,
            CreatedAt = x.ChangedAt,
            Notes = null
        };
    }

    private async Task<AuditLogDetailDto?> GetAccountStatusDetailAsync(int id)
    {
        var x = await _context.AccountStatusHistories
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.ChangedByUser)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (x == null) return null;

        return new AuditLogDetailDto
        {
            Id = x.Id,
            LogType = "AccountStatus",
            ActorId = x.ChangedBy,
            ActorName = x.ChangedByUser?.FullName,
            ActorCode = x.ChangedByUser?.Code,
            TargetUserId = x.UserId,
            TargetUserName = x.User?.FullName,
            TargetUserCode = x.User?.Code,
            ActionType = x.NewStatus,
            TargetEntity = "AccountStatus",
            TargetId = x.UserId.ToString(),
            OldValue = x.OldStatus,
            NewValue = x.NewStatus,
            RawPayload = null,
            IpAddress = null,
            UserAgent = null,
            CreatedAt = x.ChangedAt,
            Notes = string.IsNullOrEmpty(x.Reason) ? x.Notes : (string.IsNullOrEmpty(x.Notes) ? x.Reason : (x.Reason + " - " + x.Notes))
        };
    }
}

