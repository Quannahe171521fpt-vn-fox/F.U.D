using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Auth
{
    public class RefreshTokenRequestDto
    {
        [Required(ErrorMessage = "RefreshToken không được để trống")]
        public string RefreshToken { get; set; } = string.Empty;

        public string? AccessToken { get; set; }
    }
}
