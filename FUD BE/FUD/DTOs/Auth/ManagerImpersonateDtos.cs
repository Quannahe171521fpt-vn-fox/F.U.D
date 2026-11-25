using System;
using System.Collections.Generic;

namespace FUD.DTOs.Auth;

public class ImpersonationResidencyDto
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TermName { get; set; }
    public string? DormName { get; set; }
    public string? BlockName { get; set; }
    public string? RoomNumber { get; set; }
    public string? BedCode { get; set; }
    public DateOnly? CheckInDate { get; set; }
    public DateOnly? PlannedCheckOutDate { get; set; }
}

public class StudentLookupDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Gender { get; set; }
    public string AccountStatus { get; set; } = string.Empty;
    public string? SuspensionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PrimaryRole { get; set; } = "Student";
    public List<string> Roles { get; set; } = new();
    public string? Major { get; set; }
    public string? Cohort { get; set; }
    public ImpersonationResidencyDto? CurrentResidency { get; set; }
}

public class ImpersonatedStudentDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public string PrimaryRole { get; set; } = "Student";
    public List<string> Roles { get; set; } = new();
    public string AccountStatus { get; set; } = string.Empty;
    public string? SuspensionReason { get; set; }
    public string? Major { get; set; }
    public string? Cohort { get; set; }
    public int? DormId { get; set; }
    public string? DormName { get; set; }
    public ImpersonationResidencyDto? CurrentResidency { get; set; }
}

public class ImpersonationResponseDto
{
    public string ImpersonationToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; } = 1800;
    public ImpersonatedStudentDto Student { get; set; } = null!;
}

