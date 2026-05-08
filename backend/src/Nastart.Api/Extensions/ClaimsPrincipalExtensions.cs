public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this System.Security.Claims.ClaimsPrincipal user)
        => Guid.Parse("c0000000-0000-0000-0000-000000000001");
}