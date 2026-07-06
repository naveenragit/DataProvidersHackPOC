// Copy to backend/FinancialServices.Api/Services/PortfolioService.cs
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Services;

public interface IPortfolioService
{
    Task<PortfolioSummary?> GetPortfolioAsync(string portfolioId, CancellationToken ct);
    Task<string> SubmitRebalanceAsync(string portfolioId, RebalanceRequest request, CancellationToken ct);
}

/// <summary>
/// Reads portfolio data from real Azure Cosmos DB — no mock/fallback data. If the container
/// or item is missing, the caller surfaces a 404; configuration errors fail fast at startup.
/// </summary>
public sealed class PortfolioService(
    CosmosClient cosmos,
    IOptions<AzureOptions> options,
    ILogger<PortfolioService> logger) : IPortfolioService
{
    private readonly AzureOptions _options = options.Value;

    private Container Portfolios => cosmos.GetContainer(_options.CosmosDatabase, "portfolios");

    public async Task<PortfolioSummary?> GetPortfolioAsync(string portfolioId, CancellationToken ct)
    {
        // Portfolios are partitioned by /clientId, so a point read requires the clientId.
        // When only the portfolio id is known, resolve with a query on the indexed id.
        // Prefer passing clientId from the caller to enable a cheap point read:
        //   Portfolios.ReadItemAsync<PortfolioSummary>(portfolioId, new PartitionKey(clientId), ...)
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
            .WithParameter("@id", portfolioId);

        using FeedIterator<PortfolioSummary> iterator = Portfolios.GetItemQueryIterator<PortfolioSummary>(
            query, requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

        while (iterator.HasMoreResults)
        {
            FeedResponse<PortfolioSummary> page = await iterator.ReadNextAsync(ct);
            foreach (var item in page)
                return item;
        }

        logger.LogWarning("Portfolio {PortfolioId} not found", portfolioId);
        return null;
    }

    public async Task<string> SubmitRebalanceAsync(
        string portfolioId, RebalanceRequest request, CancellationToken ct)
    {
        // In a real implementation this enqueues the Microsoft Agent Framework rebalance
        // workflow (with a human-in-the-loop gate) and writes an audit record. Returns a job id.
        var jobId = Guid.NewGuid().ToString("n");
        using (logger.BeginScope(new Dictionary<string, object> { ["PortfolioId"] = portfolioId }))
        {
            logger.LogInformation("Rebalance job {JobId} accepted with {WeightCount} target weights",
                jobId, request.TargetWeights.Count);
        }
        await Task.CompletedTask;
        return jobId;
    }
}
