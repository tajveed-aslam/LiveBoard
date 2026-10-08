using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LiveBoard.Api.Models;
using LiveBoard.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LiveBoard.Api.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}

public static class AppClaims
{
    /// <summary>Present (value "true") on guest/demo tokens.</summary>
    public const string Guest = "guest";
}

public sealed class TokenService(IOptions<JwtOptions> jwtOptions, IOptions<DemoOptions> demoOptions) : ITokenService
{
    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var o = jwtOptions.Value;
        var lifetime = user.IsGuest ? demoOptions.Value.GuestTokenMinutes : o.ExpiryMinutes;
        var expiresAt = DateTime.UtcNow.AddMinutes(lifetime);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256);

        List<Claim> claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            // Carried in the token so the SignalR hub can label presence/editing without a DB lookup.
            new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
        ];
        if (user.IsGuest)
            claims.Add(new Claim(AppClaims.Guest, "true"));

        var token = new JwtSecurityToken(
            issuer: o.Issuer,
            audience: o.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
