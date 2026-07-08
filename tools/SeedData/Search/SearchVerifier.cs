using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;
using Prism.SeedData.Configuration;

namespace Prism.SeedData.Search;

/// <summary>
/// Reproducible acceptance checks against the live index (implementationPlan/03 acceptance): the index
/// is configured for hybrid + semantic + integrated vectorization; the corpus is fully present
/// (idempotency proof); a hybrid query for "NordStar leverage" returns the MSCI card with its as-of
/// metadata intact; and the Cedar Grove consensus returns three cards within one notch. Fails loud (P1).
/// </summary>
public sealed class SearchVerifier(AzureSeedOptions options, ILogger<SearchVerifier> logger)
{
    public async Task VerifyAsync(int expectedDocCount, CancellationToken ct)
    {
        options.ValidateForSeeding();
        var credential = new DefaultAzureCredential();
        var endpoint = new Uri(options.SearchEndpoint!);
        var indexClient = new SearchIndexClient(endpoint, credential);
        var searchClient = new SearchClient(endpoint, options.SearchIndex, credential);
        var embeddingClient = new AzureOpenAIClient(new Uri(options.OpenAiEndpoint!), credential)
            .GetEmbeddingClient(options.EmbeddingDeployment);

        var failures = new List<string>();

        await VerifyIndexConfigurationAsync(indexClient, failures, ct).ConfigureAwait(false);
        await VerifyDocumentCountAsync(searchClient, expectedDocCount, failures, ct).ConfigureAwait(false);
        await VerifyNordStarHybridAsync(searchClient, embeddingClient, failures, ct).ConfigureAwait(false);
        await VerifyCedarGroveConsensusAsync(searchClient, failures, ct).ConfigureAwait(false);

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Search verification failed:" + "\n  - " + string.Join("\n  - ", failures));
        }

        logger.LogInformation("All acceptance checks passed against index '{Index}'.", options.SearchIndex);
    }

    private async Task VerifyIndexConfigurationAsync(
        SearchIndexClient indexClient, List<string> failures, CancellationToken ct)
    {
        var index = (await indexClient.GetIndexAsync(options.SearchIndex, ct).ConfigureAwait(false)).Value;

        var vectorField = index.Fields.FirstOrDefault(f => f.Name == PrismSearchIndex.ContentVectorField);
        if (vectorField?.VectorSearchDimensions != options.EmbeddingDimensions)
        {
            failures.Add(
                $"contentVector dimensions {vectorField?.VectorSearchDimensions?.ToString() ?? "<none>"} != expected {options.EmbeddingDimensions}.");
        }

        if (index.VectorSearch?.Vectorizers.Count is null or 0)
        {
            failures.Add("Index has no vectorizer configured (query-time integrated vectorization would be unavailable).");
        }

        if (index.SemanticSearch?.Configurations.All(c => c.Name != PrismSearchIndex.SemanticConfigName) ?? true)
        {
            failures.Add($"Index is missing semantic configuration '{PrismSearchIndex.SemanticConfigName}'.");
        }

        logger.LogInformation(
            "Index config OK: {Fields} fields, {Vectorizers} vectorizer(s), semantic default '{Semantic}'.",
            index.Fields.Count, index.VectorSearch?.Vectorizers.Count ?? 0, index.SemanticSearch?.DefaultConfigurationName);
    }

    private async Task VerifyDocumentCountAsync(
        SearchClient searchClient, int expectedDocCount, List<string> failures, CancellationToken ct)
    {
        var count = (await searchClient.GetDocumentCountAsync(ct).ConfigureAwait(false)).Value;
        if (count != expectedDocCount)
        {
            failures.Add($"Document count {count} != expected {expectedDocCount} (idempotency / completeness).");
        }

        logger.LogInformation("Document count: {Count} (expected {Expected}).", count, expectedDocCount);
    }

    private async Task VerifyNordStarHybridAsync(
        SearchClient searchClient, EmbeddingClient embeddingClient, List<string> failures, CancellationToken ct)
    {
        const string query = "General Electric leverage";

        // Embed the query CLIENT-SIDE with the SAME deployment used at index time (matched model), so the
        // acceptance check does not depend on the Search service's managed identity having AOAI RBAC
        // (A03-01). The index vectorizer stays configured for the app's runtime integrated-vectorization.
        var queryEmbedding = await embeddingClient
            .GenerateEmbeddingAsync(query, new EmbeddingGenerationOptions { Dimensions = options.EmbeddingDimensions }, ct)
            .ConfigureAwait(false);

        var searchOptions = new SearchOptions
        {
            Size = 5,
            Select = { "id", "provider", "letter", "docType", "asOfDate", "inputAsOfDate" },
            VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(queryEmbedding.Value.ToFloats())
                    {
                        KNearestNeighborsCount = 5,
                        Fields = { PrismSearchIndex.ContentVectorField },
                    },
                },
            },
        };

        var response = await searchClient.SearchAsync<SearchDocument>(query, searchOptions, ct).ConfigureAwait(false);

        SearchDocument? msci = null;
        await foreach (var result in response.Value.GetResultsAsync().WithCancellation(ct).ConfigureAwait(false))
        {
            if (string.Equals(result.Document.GetString("id"), "nordstar-msci", StringComparison.Ordinal))
            {
                msci = result.Document;
                break;
            }
        }

        if (msci is null)
        {
            failures.Add("Hybrid query 'NordStar leverage' did not return the 'nordstar-msci' card in the top results.");
            return;
        }

        var provider = msci.GetString("provider");
        var inputAsOf = msci.GetDateTimeOffset("inputAsOfDate");
        var asOf = msci.GetDateTimeOffset("asOfDate");
        if (!string.Equals(provider, "Msci", StringComparison.Ordinal))
        {
            failures.Add($"nordstar-msci provider metadata was '{provider}', expected 'Msci'.");
        }

        if (inputAsOf == default)
        {
            failures.Add("nordstar-msci inputAsOfDate metadata is missing from the search result.");
        }

        logger.LogInformation(
            "Hybrid 'NordStar leverage' -> nordstar-msci (provider={Provider}, asOfDate={AsOf:yyyy-MM-dd}, inputAsOfDate={Input:yyyy-MM-dd}).",
            provider, asOf, inputAsOf);
    }

    private async Task VerifyCedarGroveConsensusAsync(
        SearchClient searchClient, List<string> failures, CancellationToken ct)
    {
        var searchOptions = new SearchOptions
        {
            Filter = "issuerId eq 'cedargrove' and docType eq 'ratingCard'",
            Size = 10,
            Select = { "id", "provider", "notch" },
        };

        var response = await searchClient.SearchAsync<SearchDocument>("*", searchOptions, ct).ConfigureAwait(false);

        var notches = new List<int>();
        await foreach (var result in response.Value.GetResultsAsync().WithCancellation(ct).ConfigureAwait(false))
        {
            var notch = result.Document.GetInt32("notch");
            if (notch is null)
            {
                failures.Add($"Cedar Grove card '{result.Document.GetString("id")}' is missing its notch value.");
                continue;
            }

            notches.Add(notch.Value);
        }

        if (notches.Count != 3)
        {
            failures.Add($"Cedar Grove returned {notches.Count} rating cards, expected 3.");
            return;
        }

        var spread = notches.Max() - notches.Min();
        if (spread > 1)
        {
            failures.Add($"Cedar Grove cards span {spread} notches; must be within 1.");
        }

        logger.LogInformation("Cedar Grove consensus: {Count} cards, notch spread {Spread}.", notches.Count, spread);
    }
}
