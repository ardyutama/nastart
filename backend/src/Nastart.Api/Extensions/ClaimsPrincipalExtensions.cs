using System.Security.Claims;

namespace Nastart.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    private const string SubjectClaim = "sub";
    private const string EmailClaim = "email";

    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(SubjectClaim)
            ?? throw new UnauthorizedAccessException("userId claim missing from token.");
        return Guid.Parse(value);
    }

    public static string GetEmail(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue(EmailClaim)
            ?? throw new InvalidOperationException("Email claim not found");
    }
}