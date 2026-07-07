using System.Text.Json;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace FinancialServices.Api.Infrastructure.Http;

/// <summary>
/// Single exception-handling boundary (arch-03): maps the Prism exception taxonomy to the standard
/// <c>{ "error": { "code", "message", "details" } }</c> envelope that the frontend <c>ApiError</c>
/// parses. Registered via <c>AddExceptionHandler</c> + <c>UseExceptionHandler</c>. Logs once here
/// (ids only, no PII/financials — P6). <see cref="OperationCanceledException"/> is <b>not</b> handled
/// (P7): client aborts propagate as cancellation, never a fabricated 500.
/// </summary>
public sealed class PrismExceptionHandler(ILogger<PrismExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            return false; // Let cancellation flow (P7) — do not convert to a 500.
        }

        (int status, string code, string message, IReadOnlyDictionary<string, object?>? details) = Map(exception);

        // Boundary log with context (ids/status only). 5xx is a real fault; 4xx is expected control flow.
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled {Code} on {Method} {Path} → {Status}",
                code, httpContext.Request.Method, httpContext.Request.Path, status);
        }
        else
        {
            logger.LogWarning("{Code} on {Method} {Path} → {Status}",
                code, httpContext.Request.Method, httpContext.Request.Path, status);
        }

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/json";
        var body = new ApiErrorBody(new ApiError(code, message, details));
        await httpContext.Response.WriteAsJsonAsync(body, PrismJson.Options, cancellationToken);
        return true;
    }

    private static (int Status, string Code, string Message, IReadOnlyDictionary<string, object?>? Details) Map(
        Exception ex) => ex switch
    {
        NotFoundException e => (StatusCodes.Status404NotFound, e.Code, e.Message, e.Details),
        ValidationException e => (StatusCodes.Status400BadRequest, e.Code, e.Message, e.Details),
        JsonException => (StatusCodes.Status400BadRequest, ValidationException.ErrorCode,
            "The request body could not be parsed.", null),
        UpstreamServiceException e => (StatusCodes.Status502BadGateway, e.Code, e.Message,
            new Dictionary<string, object?> { ["service"] = e.Service }),
        ConfigurationException e => (StatusCodes.Status500InternalServerError, e.Code, e.Message,
            new Dictionary<string, object?> { ["setting"] = e.Setting }),
        DomainInvariantException e => (StatusCodes.Status500InternalServerError, e.Code, e.Message,
            new Dictionary<string, object?> { ["invariant"] = e.Invariant }),
        // Fallback: never leak internals (P6) — a generic message with no exception text.
        _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR",
            "An unexpected error occurred.", null),
    };
}
