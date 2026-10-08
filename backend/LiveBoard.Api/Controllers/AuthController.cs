using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LiveBoard.Api.Data;
using LiveBoard.Api.Infrastructure;
using LiveBoard.Api.Models;
using LiveBoard.Api.Options;
using LiveBoard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LiveBoard.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AppDbContext db, ITokenService tokens, IOptions<DemoOptions> demoOptions) : ControllerBase
{
    private const string GuestEmailDomain = "guest.liveboard.local";

    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (email.EndsWith("@" + GuestEmailDomain, StringComparison.Ordinal) ||
            await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "An account with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(user);
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == email && !u.IsGuest, cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");

        return ToResponse(user);
    }

    /// <summary>Creates a throwaway guest account so visitors can try the live demo without signing up.</summary>
    [HttpPost("guest")]
    [EnableRateLimiting(RateLimiting.GuestSessionPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Guest(CancellationToken cancellationToken)
    {
        if (!demoOptions.Value.GuestAccessEnabled)
            return NotFound();

        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            Email = $"guest-{id:N}@{GuestEmailDomain}",
            // Random, never revealed: guest accounts can only be used through the issued token.
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
            IsGuest = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(user);
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<UserDto> Me() =>
        new UserDto(
            Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!),
            User.FindFirstValue(JwtRegisteredClaimNames.Email)!,
            User.HasClaim(AppClaims.Guest, "true"));

    private AuthResponse ToResponse(User user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponse(token, expiresAt, user.Email, user.IsGuest);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
