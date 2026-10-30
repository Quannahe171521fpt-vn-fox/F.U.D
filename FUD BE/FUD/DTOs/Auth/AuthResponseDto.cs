using System.Collections.Generic;

namespace FUD.DTOs.Auth;

public class AuthResponseDto
{
    public string AccessToken { get; set; } = null!;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; } = 900; // 15 phút (giây)
    public UserInfoDto User { get; set; } = null!;
}

public class UserInfoDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PrimaryRole { get; set; } = null!;
    public List<string> Roles { get; set; } = new();
}
