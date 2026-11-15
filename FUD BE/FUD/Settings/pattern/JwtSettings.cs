namespace FUD.Settings.pattern;

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "FUD-API";
    public string Audience { get; set; } = "FUD-Client";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int IdleTimeoutMinutes { get; set; } = 30;
    public int AbsoluteSessionHours { get; set; } = 24;
}
