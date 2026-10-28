namespace FUD.DTOs.Admin.Account
{
    public class RoleChangeHistoryDto
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? OldRoles { get; set; }
        public string? NewRoles { get; set; }
        public int ChangedById { get; set; }
        public string? ChangedByName { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}
