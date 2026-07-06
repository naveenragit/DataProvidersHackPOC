namespace FinancialServices.Api.Infrastructure.Errors;

/// <summary>
/// A deterministic invariant in the <c>Analysis/</c> core was violated — e.g. the attribution buckets
/// do not sum exactly to the notch gap (architecturalPlan/03 taxonomy, core principle P2). This
/// signals a <b>bug</b>, not bad input: fail loud (P1), never swallow. Middleware maps it to HTTP 500.
/// BCL-only; no connector/LLM concerns.
/// </summary>
public sealed class DomainInvariantException : Exception
{
    /// <summary>Stable, client-switchable error code for the standard error response shape.</summary>
    public const string ErrorCode = "DOMAIN_INVARIANT_VIOLATED";

    public DomainInvariantException(string invariant, string message, Exception? inner = null)
        : base(message, inner)
    {
        Invariant = invariant;
    }

    /// <summary>The invariant that was broken (e.g. <c>"attribution.sum==gap"</c>).</summary>
    public string Invariant { get; }

    /// <summary>Stable UPPER_SNAKE code surfaced to clients.</summary>
    public string Code => ErrorCode;
}
