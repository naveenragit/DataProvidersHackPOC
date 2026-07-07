using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Services;

/// <summary>
/// Cosmos-backed audit sink (container <c>audit_events</c>, partition key <c>/issuerId</c>). Each
/// event is a new immutable item. Logs through an <see cref="ILogger"/> scope carrying the issuerId,
/// emitting ids + counts only (no PII/financials — P6, financial-domain requirement).
/// </summary>
public sealed class AuditService(CosmosClient client, IOptions<AzureOptions> options, ILogger<AuditService> logger)
    : IAuditService
{
    /// <summary>Container name (partition key <c>/issuerId</c>).</summary>
    public const string ContainerName = "audit_events";

    private readonly Container _container = client.GetContainer(options.Value.CosmosDatabase, ContainerName);

    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        using (logger.BeginScope(new Dictionary<string, object> { ["issuerId"] = auditEvent.IssuerId }))
        {
            try
            {
                await _container.CreateItemAsync(
                    auditEvent, new PartitionKey(auditEvent.IssuerId), cancellationToken: ct);
            }
            catch (CosmosException ex)
            {
                throw new UpstreamServiceException("Cosmos", $"Failed to write audit event ({ex.StatusCode}).", ex);
            }

            // ids + counts only — never the dossier payload.
            logger.LogInformation(
                "Audit event {EventType}/{Action} recorded for issuer {IssuerId}",
                auditEvent.EventType, auditEvent.Action, auditEvent.IssuerId);
        }
    }
}
