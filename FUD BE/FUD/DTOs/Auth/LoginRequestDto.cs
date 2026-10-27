using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Auth
{
    public class LoginRequestDto
    {
        [Required(ErrorMessage = "Email hoặc mã đăng nhập không được để trống")]
        public string Identifier { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = false;
    }
}
