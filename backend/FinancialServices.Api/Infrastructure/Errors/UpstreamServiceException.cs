namespace FinancialServices.Api.Infrastructure.Errors;

/// <summary>
/// A call to a real upstream data service (EDGAR, FRED, …) failed. Fail-loud, no fabrication
/// (architecturalPlan/03). Middleware maps this to HTTP 502 when it lands (pkg 08/09). BCL-only.
/// </summary>
public sealed class UpstreamServiceException : Exception
{
    /// <summary>Stable, client-switchable error code for the standard error response shape.</summary>
    public const string ErrorCode = "UPSTREAM_SERVICE_FAILED";

    public UpstreamServiceException(string service, string message, Exception? inner = null)
        : base(message, inner)
    {
        Service = service;
    }

    /// <summary>The upstream service that failed (e.g. <c>"EDGAR"</c>, <c>"FRED"</c>).</summary>
    public string Service { get; }

    /// <summary>Stable UPPER_SNAKE code surfaced to clients.</summary>
    public string Code => ErrorCode;
}
