namespace FUD.DTOs.Admin.Account
{
    /// <summary>Trạng thái tài khoản sau khi thực hiện hành động (Suspend / Restore).</summary>
    public class AccountStatusResultDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public DateTime? SuspendedAt { get; set; }
        public string? SuspensionReason { get; set; }
        /// <summary>None | Sent | Failed — kết quả gửi thông báo email.</summary>
        public string NotificationStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
