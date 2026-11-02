using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Auth;

public class LoginRequestDto
{
    [Required(ErrorMessage = "Tài khoản (email) không được để trống")]
    [StringLength(100, ErrorMessage = "Tài khoản không vượt quá 100 ký tự")]
    public string Account { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [StringLength(128, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 128 ký tự")]
    public string Password { get; set; } = null!;
}
