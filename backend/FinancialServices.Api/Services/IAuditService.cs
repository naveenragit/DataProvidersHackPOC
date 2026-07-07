using FinancialServices.Api.Models;

namespace FinancialServices.Api.Services;

/// <summary>
/// Audit write boundary (arch-08: AuditService owns audit writes). Container <c>audit_events</c>,
/// partition key <c>/issuerId</c>. Records ids + counts only — never PII or raw financials (P6).
/// </summary>
public interface IAuditService
{
    /// <summary>Appends one immutable audit event.</summary>
    Task WriteAsync(AuditEvent auditEvent, CancellationToken ct);
}
