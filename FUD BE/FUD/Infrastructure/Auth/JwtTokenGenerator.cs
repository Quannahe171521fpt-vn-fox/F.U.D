using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FUD.Models;
using FUD.Settings.pattern;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FUD.Infrastructure.Auth;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _jwtSettings;
    private readonly byte[] _keyBytes;

    public JwtTokenGenerator(IOptions<JwtSettings> jwtOptions)
    {
        _jwtSettings = jwtOptions.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
        if (string.IsNullOrWhiteSpace(_jwtSettings.Key))
            throw new InvalidOperationException("Cấu hình 'Jwt:Key' không được để trống!");

        var keyStr = _jwtSettings.Key;
        try
        {
            _keyBytes = Convert.FromBase64String(keyStr);
        }
        catch (FormatException)
        {
            _keyBytes = Encoding.UTF8.GetBytes(keyStr);
        }
    }

    public string GenerateAccessToken(User user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("sub", user.Id.ToString()),
            new("nameid", user.Id.ToString()),
            new("user_code", user.Code),
            new(ClaimTypes.Email, user.Email),
            new("email", user.Email),
            new(ClaimTypes.Name, user.FullName)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        var key = new SymmetricSecurityKey(_keyBytes);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var accessMinutes = _jwtSettings.AccessTokenExpirationMinutes > 0
            ? _jwtSettings.AccessTokenExpirationMinutes
            : 15;
        var issuer = string.IsNullOrWhiteSpace(_jwtSettings.Issuer) ? "FUD-API" : _jwtSettings.Issuer;
        var audience = string.IsNullOrWhiteSpace(_jwtSettings.Audience) ? "FUD-Client" : _jwtSettings.Audience;

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(accessMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateImpersonationToken(User student, IEnumerable<string> roles, int managerId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, student.Id.ToString()),
            new("sub", student.Id.ToString()),
            new("nameid", student.Id.ToString()),
            new("user_code", student.Code),
            new(ClaimTypes.Email, student.Email),
            new("email", student.Email),
            new(ClaimTypes.Name, student.FullName),
            new("impersonated_by", managerId.ToString()),
            new("imp", "true")
        };

        claims.Add(new Claim(ClaimTypes.Role, "Student"));
        claims.Add(new Claim("role", "Student"));

        var key = new SymmetricSecurityKey(_keyBytes);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var issuer = string.IsNullOrWhiteSpace(_jwtSettings.Issuer) ? "FUD-API" : _jwtSettings.Issuer;
        var audience = string.IsNullOrWhiteSpace(_jwtSettings.Audience) ? "FUD-Client" : _jwtSettings.Audience;

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string RawToken, string TokenHash) GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var rawToken = Convert.ToBase64String(randomBytes);
        var tokenHash = HashToken(rawToken);
        return (rawToken, tokenHash);
    }

    public string HashToken(string rawToken)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
