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
        public bool ConfirmedDuplicate { get; set; } = false;
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
        public string Status { get; set; } = string.Empty;
        public string? RecordedByName { get; set; }
        public List<ViolationEvidenceDto> Evidences { get; set; } = new List<ViolationEvidenceDto>();
    }

    public class DuplicateViolationCheckResultDto
    {
        public bool HasDuplicate { get; set; }
        public List<ViolationItemDto> ExistingViolations { get; set; } = new List<ViolationItemDto>();
        public string Message { get; set; } = string.Empty;
    }
}
