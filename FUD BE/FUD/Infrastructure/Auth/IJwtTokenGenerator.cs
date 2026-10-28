namespace FUD.Infrastructure.Auth
{
    public interface IJwtTokenGenerator
    {
        /// <summary>Tạo access token chứa userId, roles và token version.</summary>
        (string Token, DateTime ExpiresAt) GenerateAccessToken(int userId, string username, IEnumerable<string> roles, int tokenVersion = 0);

        /// <summary>Tạo refresh token ngẫu nhiên (dạng base64url), dùng để lưu hash trong DB.</summary>
        string GenerateRefreshToken();

        /// <summary>Hash SHA-256 của refresh token (lưu vào DB).</summary>
        string HashToken(string token);
    }
}
