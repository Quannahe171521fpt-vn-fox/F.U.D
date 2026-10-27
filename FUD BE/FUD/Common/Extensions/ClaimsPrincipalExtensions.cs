using System.Security.Claims;

namespace FUD.Common.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static int? GetUserId(this ClaimsPrincipal principal)
        {
            var claim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out var id))
            {
                return id;
            }
            return null;
        }

        public static string? GetGender(this ClaimsPrincipal principal)
        {
            return principal.FindFirst("gender")?.Value ?? principal.FindFirst(ClaimTypes.Gender)?.Value;
        }

        public static string? GetCode(this ClaimsPrincipal principal)
        {
            return principal.FindFirst("code")?.Value;
        }

        public static string? GetEmail(this ClaimsPrincipal principal)
        {
            return principal.FindFirst(ClaimTypes.Email)?.Value;
        }

        public static IEnumerable<string> GetRoles(this ClaimsPrincipal principal)
        {
            return principal.FindAll(ClaimTypes.Role).Select(c => c.Value);
        }

        public static bool IsStudent(this ClaimsPrincipal principal)
        {
            return principal.IsInRole("Student");
        }
    }
}
