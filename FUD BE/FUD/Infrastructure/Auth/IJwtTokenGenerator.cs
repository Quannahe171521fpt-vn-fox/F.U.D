using System.Collections.Generic;
using FUD.Models;

namespace FUD.Infrastructure.Auth;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user, IEnumerable<string> roles);
    string GenerateImpersonationToken(User student, IEnumerable<string> roles, int managerId);
    (string RawToken, string TokenHash) GenerateRefreshToken();
    string HashToken(string rawToken);
}
