namespace FUD.Infrastructure.Auth;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
    bool VerifyDummy();
}
