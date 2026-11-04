using System.Security.Claims;

namespace CircDesk.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static int GetMemberId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : throw new InvalidOperationException("El token no contiene un id de socio válido.");
    }

    public static bool IsLibrarian(this ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(Domain.MemberRole.Librarian));
}
