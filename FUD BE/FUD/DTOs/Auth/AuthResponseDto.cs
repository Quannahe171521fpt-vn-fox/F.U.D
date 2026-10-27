using System;

namespace FUD.DTOs.Auth
{
    public class AuthResponseDto
    {
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public UserDto? User { get; set; }
    }
}
