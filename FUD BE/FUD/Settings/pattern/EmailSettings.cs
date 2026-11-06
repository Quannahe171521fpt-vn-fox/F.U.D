namespace FUD.Settings.pattern
{
    /// <summary>Cấu hình gửi email qua Gmail SMTP.</summary>
    public class EmailSettings
    {
        /// <summary>Địa chỉ Gmail dùng để gửi (ví dụ: no-reply@fud.edu.vn).</summary>
        public string SenderEmail { get; set; } = string.Empty;

        /// <summary>Mật khẩu ứng dụng (App Password) của tài khoản Gmail.</summary>
        public string SenderPassword { get; set; } = string.Empty;

        /// <summary>Ten hiển thị của người gửi.</summary>
        public string SenderName { get; set; } = "FPT University Dormitory";

        /// <summary>Host SMTP. Mặc định là smtp.gmail.com.</summary>
        public string SmtpHost { get; set; } = "smtp.gmail.com";

        /// <summary>Cổng SMTP. Mặc định là 587.</summary>
        public int SmtpPort { get; set; } = 587;

        /// <summary>Bật/tắt gửi email. Hữu ích cho môi trường Development hoặc Unit Test.</summary>
        public bool Enabled { get; set; } = true;
    }
}
