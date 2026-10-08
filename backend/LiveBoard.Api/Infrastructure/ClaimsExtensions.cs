using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LiveBoard.Api.Models;
using LiveBoard.Api.Services;

namespace LiveBoard.Api.Infrastructure;

public static class ClaimsExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated user has no 'sub' claim."));

    public static string GetDisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Name) ?? "Someone";

    public static bool IsGuest(this ClaimsPrincipal user) => user.HasClaim(AppClaims.Guest, "true");

    public static Actor GetActor(this ClaimsPrincipal user) => new(user.GetUserId(), user.GetDisplayName());
}
