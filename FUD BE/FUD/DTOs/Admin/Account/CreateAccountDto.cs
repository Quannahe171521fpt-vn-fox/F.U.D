using System.ComponentModel.DataAnnotations;
using FUD.Common;

namespace FUD.DTOs.Admin.Account
{
    /// <summary>Body cho POST /api/admin/users — Tạo tài khoản mới</summary>
    public class CreateAccountDto
    {
        [Required(ErrorMessage = "Username là bắt buộc")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username phải từ 3 đến 50 ký tự")]
        [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Username chỉ gồm chữ, số và các ký tự . _ -")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ tên là bắt buộc")]
        [StringLength(100, ErrorMessage = "Họ tên không vượt quá 100 ký tự")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        [StringLength(100, ErrorMessage = "Email không vượt quá 100 ký tự")]
        public string Email { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "Số điện thoại không vượt quá 20 ký tự")]
        public string? Phone { get; set; }

        /// <summary>Mật khẩu tạm: tối thiểu 8 ký tự, có chữ hoa, chữ thường và số.</summary>
        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự")]
        [RegularExpression("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).+$", ErrorMessage = "Mật khẩu phải có chữ hoa, chữ thường và chữ số")]
        public string Password { get; set; } = string.Empty;

        /// <summary>Vai trò ban đầu — chỉ nhận 1 trong: Student, Manager, Guard, Admin.</summary>
        [Required(ErrorMessage = "Vai trò ban đầu là bắt buộc")]
        [CustomValidation(typeof(AccountValidation), nameof(AccountValidation.ValidRoleName))]
        public string RoleName { get; set; } = string.Empty;
    }
}
