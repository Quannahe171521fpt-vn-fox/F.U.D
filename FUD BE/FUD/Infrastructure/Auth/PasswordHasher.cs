namespace FUD.Infrastructure.Auth;

public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;
    private const string DummyHash = "$2a$12$iJTeNebFgmMKv1NLJAasAOx4AthM65ws5RQiqh/ngAEca61BAeI8S";

    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }

    public bool VerifyDummy()
    {
        return BCrypt.Net.BCrypt.Verify("Dummy@Password!", DummyHash);
    }
}
