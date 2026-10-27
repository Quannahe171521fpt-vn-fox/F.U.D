using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FUD.DTOs.Auth;
using FUD.Models;
using FUD.Services.Interfaces;
using FUD.Settings.pattern;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FUD.Services
{
    public class AuthService : IAuthService
    {
        private readonly FudContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<AuthService> _logger;

        public AuthService(FudContext context, IOptions<JwtSettings> jwtOptions, ILogger<AuthService> logger)
        {
            _context = context;
            _jwtSettings = jwtOptions.Value;
            _logger = logger;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, string? ipAddress = null, string? userAgent = null)
        {
            var user = await _context.Users
                .Include(u => u.UserRoleUsers)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.Student)
                    .ThenInclude(s => s!.Residencies)
                        .ThenInclude(r => r.Bed)
                            .ThenInclude(b => b.Room)
                                .ThenInclude(rm => rm.Block)
                .FirstOrDefaultAsync(u => u.Email == request.Identifier || u.Code == request.Identifier);

            if (user == null)
            {
                throw new UnauthorizedAccessException("Tài khoản hoặc mật khẩu không chính xác.");
            }

            if (string.Equals(user.AccountStatus, "Suspended", StringComparison.OrdinalIgnoreCase))
            {
                var reason = string.IsNullOrWhiteSpace(user.SuspensionReason)
                    ? "Vui lòng liên hệ Ban Quản lý KTX."
                    : user.SuspensionReason;
                throw new InvalidOperationException($"Tài khoản của bạn đã bị tạm khóa. Lý do: {reason}");
            }

            bool isPasswordValid = false;
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                // 1. Kiểm tra nếu là hash BCrypt ($2a$, $2b$, $2y$)
                if (user.PasswordHash.StartsWith("$2"))
                {
                    try
                    {
                        isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Lỗi kiểm tra PasswordHash bằng BCrypt cho User {UserId}", user.Id);
                        isPasswordValid = false;
                    }
                }

                // 2. Fallback cho dữ liệu seed ban đầu (như 'hash123' hoặc plain text)
                if (!isPasswordValid && string.Equals(user.PasswordHash, request.Password, StringComparison.Ordinal))
                {
                    isPasswordValid = true;
                    // Tự động nâng cấp băm BCrypt vào DB
                    try
                    {
                        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Đã tự động nâng cấp mã hóa BCrypt cho User {UserId}", user.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Không thể tự động băm mật khẩu cho User {UserId}", user.Id);
                    }
                }
            }

            if (!isPasswordValid)
            {
                throw new UnauthorizedAccessException("Tài khoản hoặc mật khẩu không chính xác.");
            }

            var tokenResult = GenerateTokens(user);

            // Ghi nhận Audit Log
            try
            {
                var auditLog = new AuditLog
                {
                    UserId = user.Id,
                    ActionType = "LOGIN_SUCCESS",
                    TargetEntity = "User",
                    TargetId = user.Id.ToString(),
                    NewValue = $"Đăng nhập thành công qua mật khẩu. IP: {ipAddress}",
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    CreatedAt = DateTime.UtcNow
                };
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi ghi nhận Audit Log cho User {UserId}", user.Id);
            }

            return new AuthResponseDto
            {
                Success = true,
                Message = "Đăng nhập thành công",
                AccessToken = tokenResult.AccessToken,
                RefreshToken = tokenResult.RefreshToken,
                ExpiresAt = tokenResult.ExpiresAt,
                User = MapToUserDto(user)
            };
        }

        public async Task<AuthResponseDto> LoginGoogleAsync(GoogleLoginRequestDto request, string? ipAddress = null, string? userAgent = null)
        {
            GoogleJsonWebSignature.Payload payload;
            try
            {
                // Xác thực Google IdToken
                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google IdToken không hợp lệ");
                throw new UnauthorizedAccessException("Google Token không hợp lệ hoặc đã hết hạn.");
            }

            var email = payload.Email;
            var googleId = payload.Subject;

            var user = await _context.Users
                .Include(u => u.UserRoleUsers)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.Student)
                    .ThenInclude(s => s!.Residencies)
                        .ThenInclude(r => r.Bed)
                            .ThenInclude(b => b.Room)
                                .ThenInclude(rm => rm.Block)
                .FirstOrDefaultAsync(u => u.GoogleId == googleId || u.Email == email);

            if (user == null)
            {
                throw new UnauthorizedAccessException("Email Google này chưa được đăng ký trong hệ thống KTX. Vui lòng liên hệ BQL.");
            }

            // Cập nhật GoogleId nếu chưa có
            if (string.IsNullOrEmpty(user.GoogleId))
            {
                user.GoogleId = googleId;
                await _context.SaveChangesAsync();
            }

            if (string.Equals(user.AccountStatus, "Suspended", StringComparison.OrdinalIgnoreCase))
            {
                var reason = string.IsNullOrWhiteSpace(user.SuspensionReason)
                    ? "Vui lòng liên hệ Ban Quản lý KTX."
                    : user.SuspensionReason;
                throw new InvalidOperationException($"Tài khoản của bạn đã bị tạm khóa. Lý do: {reason}");
            }

            var tokenResult = GenerateTokens(user);

            // Ghi nhận Audit Log
            try
            {
                var auditLog = new AuditLog
                {
                    UserId = user.Id,
                    ActionType = "LOGIN_GOOGLE_SUCCESS",
                    TargetEntity = "User",
                    TargetId = user.Id.ToString(),
                    NewValue = $"Đăng nhập thành công qua Google ({email}). IP: {ipAddress}",
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    CreatedAt = DateTime.UtcNow
                };
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi ghi nhận Audit Log cho User {UserId}", user.Id);
            }

            return new AuthResponseDto
            {
                Success = true,
                Message = "Đăng nhập Google thành công",
                AccessToken = tokenResult.AccessToken,
                RefreshToken = tokenResult.RefreshToken,
                ExpiresAt = tokenResult.ExpiresAt,
                User = MapToUserDto(user)
            };
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
        {
            var principal = GetPrincipalFromExpiredToken(request.AccessToken);
            if (principal == null)
            {
                throw new UnauthorizedAccessException("Access Token không hợp lệ.");
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("Không xác định được danh tính người dùng.");
            }

            var user = await _context.Users
                .Include(u => u.UserRoleUsers)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.Student)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || string.Equals(user.AccountStatus, "Suspended", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Tài khoản không tồn tại hoặc đã bị khóa.");
            }

            var tokenResult = GenerateTokens(user);

            return new AuthResponseDto
            {
                Success = true,
                Message = "Làm mới phiên đăng nhập thành công",
                AccessToken = tokenResult.AccessToken,
                RefreshToken = tokenResult.RefreshToken,
                ExpiresAt = tokenResult.ExpiresAt,
                User = MapToUserDto(user)
            };
        }

        public async Task<UserDto> GetCurrentUserProfileAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.UserRoleUsers)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.Student)
                    .ThenInclude(s => s!.Residencies)
                        .ThenInclude(r => r.Bed)
                            .ThenInclude(b => b.Room)
                                .ThenInclude(rm => rm.Block)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                throw new KeyNotFoundException("Không tìm thấy thông tin người dùng.");
            }

            return MapToUserDto(user);
        }

        #region Private Helpers

        private (string AccessToken, string RefreshToken, DateTime ExpiresAt) GenerateTokens(User user)
        {
            var keyBytes = Encoding.UTF8.GetBytes(_jwtSettings.Key);
            var symmetricKey = new SymmetricSecurityKey(keyBytes);
            var credentials = new SigningCredentials(symmetricKey, SecurityAlgorithms.HmacSha256);

            var roles = user.UserRoleUsers.Select(ur => ur.Role.RoleName).ToList();

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("code", user.Code),
                new Claim("gender", user.Gender),
                new Claim(ClaimTypes.Gender, user.Gender)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            if (user.Student != null && !string.IsNullOrEmpty(user.Student.Cohort))
            {
                claims.Add(new Claim("cohort", user.Student.Cohort));
            }

            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var accessTokenString = tokenHandler.WriteToken(token);

            // Sinh chuỗi ngẫu nhiên cho Refresh Token
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            var refreshTokenString = Convert.ToBase64String(randomNumber);

            return (accessTokenString, refreshTokenString, expiresAt);
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string? token)
        {
            if (string.IsNullOrEmpty(token)) return null;

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key)),
                ValidateLifetime = false // Bỏ qua hạn khi đọc token cũ để refresh
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }

        private static UserDto MapToUserDto(User user)
        {
            var activeResidency = user.Student?.Residencies
                .FirstOrDefault(r => string.Equals(r.Status, "Active", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(r.Status, "CheckedIn", StringComparison.OrdinalIgnoreCase));

            StudentProfileDto? studentInfo = null;
            if (user.Student != null)
            {
                studentInfo = new StudentProfileDto
                {
                    Major = user.Student.Major,
                    Cohort = user.Student.Cohort,
                    CurrentBedId = activeResidency?.BedId,
                    CurrentBedNumber = activeResidency?.Bed?.BedCode,
                    CurrentRoomCode = activeResidency?.Bed?.Room?.RoomNumber,
                    CurrentBlockName = activeResidency?.Bed?.Room?.Block?.Name
                };
            }

            return new UserDto
            {
                Id = user.Id,
                Code = user.Code,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Gender = user.Gender,
                AccountStatus = user.AccountStatus,
                Roles = user.UserRoleUsers.Select(ur => ur.Role.RoleName).ToList(),
                StudentInfo = studentInfo
            };
        }

        #endregion
    }
}
