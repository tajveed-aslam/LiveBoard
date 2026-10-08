using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LiveBoard.Api.Infrastructure;

/// <summary>The request can't be applied as sent (empty title, limit reached, unknown column...). Maps to 400.</summary>
public sealed class InputValidationException(string message) : Exception(message);

/// <summary>
/// The resource doesn't exist, or the caller isn't a member of its board. Deliberately the same response for
/// both, so board ids can't be probed. Maps to 404.
/// </summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>The caller is a member but lacks the role (e.g. only the owner can delete a board). Maps to 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            InputValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Not allowed"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error"),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");
        else
            logger.LogInformation("{Title}: {Message}", title, exception.Message);

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Never echo internal exception details for unexpected errors.
                Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
            },
        });
    }
}
