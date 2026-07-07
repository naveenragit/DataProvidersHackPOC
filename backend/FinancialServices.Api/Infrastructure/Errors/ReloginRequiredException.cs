namespace FinancialServices.Api.Infrastructure.Errors;

/// <summary>
/// A live rating-provider needs a one-time interactive OAuth login that cannot be performed in the
/// current (headless) context (architecturalPlan/03 taxonomy; STK-03). Thrown when a provider is
/// enabled but its token cache is empty/expired and no interactive browser flow is available — the
/// runtime redirect delegate raises this instead of blocking on <c>Console.ReadLine</c>, so a dead
/// refresh token degrades <b>loud</b> at the login/ingestion boundary (P1) and never wedges a request.
/// BCL-only; the ingestion/CLI boundary surfaces it with the re-login runbook.
/// </summary>
public sealed class ReloginRequiredException : Exception
{
    /// <summary>Stable, client-switchable error code for the standard error response shape.</summary>
    public const string ErrorCode = "RELOGIN_REQUIRED";

    public ReloginRequiredException(string provider, string message, Exception? inner = null)
        : base(message, inner)
    {
        Provider = provider;
    }

    /// <summary>The provider whose interactive login is required (e.g. <c>"Morningstar"</c>).</summary>
    public string Provider { get; }

    /// <summary>Stable UPPER_SNAKE code surfaced to clients.</summary>
    public string Code => ErrorCode;
}
