namespace FUD.Infrastructure.Security
{
    /// <summary>Sử dụng BCrypt với work factor 11.</summary>
    public class BcryptPasswordHasher : IPasswordHasher
    {
        private const int WorkFactor = 11;

        public string Hash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
        }

        public bool Verify(string password, string passwordHash)
        {
            if (string.IsNullOrEmpty(passwordHash))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, passwordHash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return false;
            }
        }
    }
}
