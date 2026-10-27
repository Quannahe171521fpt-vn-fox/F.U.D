namespace FUD.DTOs.Admin.Account
{
    /// <summary>Chi tiết tài khoản (không bao giờ trả PasswordHash).</summary>
    public class AccountDetailDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? SuspendedAt { get; set; }
        public string? SuspensionReason { get; set; }
        public int? SuspendedById { get; set; }
        public string? SuspendedByName { get; set; }
        public List<AccountStatusHistoryDto> StatusHistories { get; set; } = new List<AccountStatusHistoryDto>();
        public List<RoleChangeHistoryDto> RoleHistories { get; set; } = new List<RoleChangeHistoryDto>();
    }
}
