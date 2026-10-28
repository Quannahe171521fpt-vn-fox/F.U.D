namespace FUD.Settings.pattern
{
    /// <summary>
    /// Thông tin tài khoản Admin mặc định, đọc từ user-secrets / appsettings / biến môi trường.
    /// Không hard-code mật khẩu trong source code.
    /// </summary>
    public class SeedAdminSettings
    {
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
