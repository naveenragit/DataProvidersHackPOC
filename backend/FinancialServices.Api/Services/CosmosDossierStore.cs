using System.Net;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Services;

/// <summary>
/// Cosmos-backed dossier store. Always passes the partition key for point operations (arch-08 — never
/// a cross-partition scan). Cosmos failures fail loud as <see cref="UpstreamServiceException"/>; a
/// <c>404</c> read is <c>null</c> (a legitimate "not found", surfaced by the caller as HTTP 404).
/// </summary>
public sealed class CosmosDossierStore : ICosmosDossierStore
{
    /// <summary>Container name (partition key <c>/issuerId</c>).</summary>
    public const string ContainerName = "rating_reconciliations";

    private readonly Container _container;

    public CosmosDossierStore(CosmosClient client, IOptions<AzureOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        _container = client.GetContainer(options.Value.CosmosDatabase, ContainerName);
    }

    public async Task UpsertAsync(ReconciliationDossier dossier, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        try
        {
            await _container.UpsertItemAsync(
                dossier, new PartitionKey(dossier.IssuerId), cancellationToken: ct);
        }
        catch (CosmosException ex)
        {
            throw new UpstreamServiceException("Cosmos", $"Failed to persist dossier ({ex.StatusCode}).", ex);
        }
    }

    public async Task<ReconciliationDossier?> ReadAsync(string id, string issuerId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerId);
        try
        {
            ItemResponse<ReconciliationDossier> response =
                await _container.ReadItemAsync<ReconciliationDossier>(
                    id, new PartitionKey(issuerId), cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (CosmosException ex)
        {
            throw new UpstreamServiceException("Cosmos", $"Failed to read dossier ({ex.StatusCode}).", ex);
        }
    }
}
