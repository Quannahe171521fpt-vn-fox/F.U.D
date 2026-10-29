using System.Collections.Generic;
using FUD.Models;

namespace FUD.Infrastructure.Auth;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user, IEnumerable<string> roles);
    (string RawToken, string TokenHash) GenerateRefreshToken();
    string HashToken(string rawToken);
}
