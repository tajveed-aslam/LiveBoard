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
public sealed class AuthController(AppDbContext db, ITokenService tokens, BoardService boards, IOptions<DemoOptions> demoOptions)
    : ControllerBase
{
    private const string GuestEmailDomain = "guest.liveboard.local";

    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        if (email.EndsWith("@" + GuestEmailDomain, StringComparison.Ordinal) ||
            await db.Users.AnyAsync(u => u.Email == email, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "An account with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = DisplayNames.ForUser(request.DisplayName, email),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return ToResponse(user);
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == email && !u.IsGuest, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");

        return ToResponse(user);
    }

    /// <summary>
    /// Creates a throwaway guest account (with a friendly random name and a sample board) so visitors can try
    /// the live demo without signing up.
    /// </summary>
    [HttpPost("guest")]
    [EnableRateLimiting(RateLimiting.GuestSessionPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Guest([FromQuery] bool withSampleBoard = true, CancellationToken ct = default)
    {
        if (!demoOptions.Value.GuestAccessEnabled)
            return NotFound();

        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            Email = $"guest-{id:N}@{GuestEmailDomain}",
            DisplayName = DisplayNames.ForGuest(),
            // Random, never revealed: guest accounts can only be used through the issued token.
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
            IsGuest = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        // Guests arriving through someone's share link don't need a board of their own.
        if (withSampleBoard)
            await boards.CreateSampleBoardAsync(user.Id, ct);

        return ToResponse(user);
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<UserDto> Me() =>
        new UserDto(User.GetUserId(), User.FindFirst("email")?.Value ?? "", User.GetDisplayName(), User.IsGuest());

    private AuthResponse ToResponse(User user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponse(token, expiresAt, user.Id, user.Email, user.DisplayName, user.IsGuest);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
