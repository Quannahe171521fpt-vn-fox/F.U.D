namespace FUD.DTOs.Auth
{
    public class StudentProfileDto
    {
        public string? Major { get; set; }
        public string? Cohort { get; set; }
        public int? CurrentBedId { get; set; }
        public string? CurrentBedNumber { get; set; }
        public string? CurrentRoomCode { get; set; }
        public string? CurrentBlockName { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Gender { get; set; } = string.Empty; // "Male" or "Female"
        public string AccountStatus { get; set; } = "Active";
        public List<string> Roles { get; set; } = new();
        public StudentProfileDto? StudentInfo { get; set; }
    }
}
