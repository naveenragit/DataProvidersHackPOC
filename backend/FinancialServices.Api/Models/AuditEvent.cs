namespace FinancialServices.Api.Models;

/// <summary>
/// A consequential-action audit record (financial-domain requirement; spec §C). Written to the
/// Cosmos <c>audit_events</c> container (partition key <c>/issuerId</c>) on scope-confirmed / dossier
/// generated / dossier exported. <b>Never</b> carries PII or raw financials (P6): <see cref="Metadata"/>
/// holds ids + counts only.
/// </summary>
public sealed record AuditEvent(
    string Id,                                       // "{issuerId}:{guid}" — partition-addressable
    string EventType,                                // e.g. "reconciliation"
    DateTimeOffset Timestamp,
    string Actor,                                    // "system" for the demo; the authenticated user in prod
    string IssuerId,                                 // partition key
    string Action,                                   // "dossier_generated" | "dossier_exported"
    IReadOnlyDictionary<string, object> Metadata);   // ids + counts ONLY (no PII/financials)
