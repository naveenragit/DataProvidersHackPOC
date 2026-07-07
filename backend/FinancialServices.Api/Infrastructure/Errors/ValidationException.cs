namespace FinancialServices.Api.Infrastructure.Errors;

/// <summary>
/// The request payload is invalid — a missing/blank required field, an unknown field, or an
/// unparseable id (architecturalPlan/03 taxonomy). Middleware maps it to HTTP 400 with the standard
/// error envelope. <see cref="Details"/> carries only safe field-level messages (no payload values,
/// never PII/financials — P6). BCL-only.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>Stable, client-switchable error code for the standard error response shape.</summary>
    public const string ErrorCode = "VALIDATION_FAILED";

    public ValidationException(string message, IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Details = details;
    }

    /// <summary>Stable UPPER_SNAKE code surfaced to clients.</summary>
    public string Code => ErrorCode;

    /// <summary>Safe field-level context for the error envelope's <c>details</c> (no payload values).</summary>
    public IReadOnlyDictionary<string, object?>? Details { get; }
}
