using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using LiveBoard.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LiveBoard.Api.Infrastructure;

/// <summary>Protects a public demo: per-user write limits and per-IP guest sign-ups.</summary>
public static class RateLimiting
{
    public const string WritePolicy = "write";
    public const string GuestSessionPolicy = "guest-session";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, DemoOptions demo)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(WritePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"write:{context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? ClientIp(context)}",
                    _ => HourlyWindow(demo.WritesPerHour)));

            options.AddPolicy(GuestSessionPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"guest:{ClientIp(context)}",
                    _ => HourlyWindow(demo.GuestSessionsPerHourPerIp)));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

                await response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "You've hit the hourly limit for this demo. Please try again later.",
                }, cancellationToken);
            };
        });

        return services;
    }

    private static FixedWindowRateLimiterOptions HourlyWindow(int permits) => new()
    {
        PermitLimit = Math.Max(permits, 1),
        Window = TimeSpan.FromHours(1),
        QueueLimit = 0,
    };

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
