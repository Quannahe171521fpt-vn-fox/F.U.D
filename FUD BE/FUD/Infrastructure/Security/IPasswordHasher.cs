namespace FUD.Infrastructure.Security
{
    /// <summary>Hash / verify mật khẩu. Chỉ lưu hash trong DB, không bao giờ lưu mật khẩu rõ.</summary>
    public interface IPasswordHasher
    {
        string Hash(string password);

        bool Verify(string password, string passwordHash);
    }
}
