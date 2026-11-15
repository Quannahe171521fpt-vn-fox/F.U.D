namespace FUD.DTOs.Violations
{
    public class ViolationTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class RoomStudentDto
    {
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string BedCode { get; set; } = string.Empty;
        public string StatusDescription { get; set; } = string.Empty;
        public int? ResidencyId { get; set; }
    }

    public class CreateViolationRequestDto
    {
        public int RoomId { get; set; }
        public List<int> StudentIds { get; set; } = new List<int>();
        public int ViolationTypeId { get; set; }
        public DateTime OccurrenceTime { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<IFormFile> Evidences { get; set; } = new List<IFormFile>();
    }

    public class ViolationEvidenceDto
    {
        public int Id { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }

    public class ViolationItemDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public int ViolationTypeId { get; set; }
        public string ViolationTypeName { get; set; } = string.Empty;
        public DateTime OccurrenceTime { get; set; }
        public string? Description { get; set; }
        public string? InitialLevel { get; set; }
        public string? FinalLevel { get; set; }
        public string? ManagerNote { get; set; }
        public DateTime? RequestDeadline { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? RecordedByName { get; set; }
        public string? EvaluatedByName { get; set; }
        public DateTime? FinalizedTime { get; set; }
        public string? TermName { get; set; }
        public List<ViolationEvidenceDto> Evidences { get; set; } = new List<ViolationEvidenceDto>();
        public int PriorViolationsCount { get; set; }

        // Trạng thái tài khoản & liên kết đơn khiếu nại (Request Complaint)
        public string? StudentAccountStatus { get; set; }
        public string? SuspensionReason { get; set; }
        public int? LinkedRequestId { get; set; }
    }

    public class EvaluateViolationRequestDto
    {
        public string InitialLevel { get; set; } = string.Empty;
        public string? ManagerNote { get; set; }
    }

    public class UnsuspendStudentDto
    {
        public string? Note { get; set; }
    }

    public class BlockForViolationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int DormId { get; set; }
        public string DormName { get; set; } = string.Empty;
    }
}
