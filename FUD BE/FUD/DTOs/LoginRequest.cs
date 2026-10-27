using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FUD.DTOs
{
    public class LoginRequest
    {
        /// <summary>Username hoặc email.</summary>
        [Required(ErrorMessage = "Tài khoản là bắt buộc")]
        [JsonPropertyName("usernameOrEmail")]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        [Required(ErrorMessage = "Refresh token là bắt buộc")]
        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
