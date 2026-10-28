namespace FUD.DTOs.Admin.Account
{
    /// <summary>Một vai trò có thể cấp cho tài khoản.</summary>
    public class RoleOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
