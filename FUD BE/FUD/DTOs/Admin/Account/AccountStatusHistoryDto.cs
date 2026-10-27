namespace FUD.DTOs.Admin.Account
{
    public class AccountStatusHistoryDto
    {
        public int Id { get; set; }
        public string OldStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public int ChangedById { get; set; }
        public string? ChangedByName { get; set; }
        public DateTime ChangedAt { get; set; }
        public string NotificationStatus { get; set; } = string.Empty;
    }
}
