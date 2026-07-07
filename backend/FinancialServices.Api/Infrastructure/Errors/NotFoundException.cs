namespace FinancialServices.Api.Infrastructure.Errors;

/// <summary>
/// A requested resource (issuer, dossier) does not exist (architecturalPlan/03 taxonomy). Middleware
/// maps it to HTTP 404 with the standard error envelope. Carries only safe context (resource kind +
/// id) — ids are public identifiers, never PII (P6). BCL-only.
/// </summary>
public sealed class NotFoundException : Exception
{
    /// <summary>Stable, client-switchable error code for the standard error response shape.</summary>
    public const string ErrorCode = "NOT_FOUND";

    public NotFoundException(string resource, string resourceId, string? message = null)
        : base(message ?? $"{resource} '{resourceId}' was not found.")
    {
        Resource = resource;
        ResourceId = resourceId;
    }

    /// <summary>The kind of resource that was missing (e.g. <c>"issuer"</c>, <c>"dossier"</c>).</summary>
    public string Resource { get; }

    /// <summary>The id that was not found (a public identifier — not PII).</summary>
    public string ResourceId { get; }

    /// <summary>Stable UPPER_SNAKE code surfaced to clients.</summary>
    public string Code => ErrorCode;

    /// <summary>Safe context (ids only) for the error envelope's <c>details</c>.</summary>
    public IReadOnlyDictionary<string, object?> Details =>
        new Dictionary<string, object?> { ["resource"] = Resource, ["id"] = ResourceId };
}
