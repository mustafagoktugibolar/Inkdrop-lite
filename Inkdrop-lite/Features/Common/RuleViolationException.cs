using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Inkdrop_lite.Features.Common;

/// <summary>A business rule rejected the request; surfaced as ProblemDetails with the given status.</summary>
public sealed class RuleViolationException(string message, int statusCode = StatusCodes.Status400BadRequest)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static RuleViolationException Conflict(string message) =>
        new(message, StatusCodes.Status409Conflict);
}

public sealed class RuleViolationExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not RuleViolationException violation)
        {
            return false;
        }

        httpContext.Response.StatusCode = violation.StatusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = violation.StatusCode,
                Title = violation.StatusCode switch
                {
                    StatusCodes.Status409Conflict => "Conflict",
                    StatusCodes.Status413PayloadTooLarge => "Payload Too Large",
                    _ => "Bad Request"
                },
                Detail = violation.Message
            }
        });
    }
}
