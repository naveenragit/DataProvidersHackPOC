using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;
using Prism.SeedData.Configuration;
using Prism.SeedData.Model;

namespace Prism.SeedData.Search;

/// <summary>
/// Creates/updates the <c>prism-ratings</c> index, embeds each document's content with the configured
/// Azure OpenAI deployment (1536 dims), and upserts the corpus with <c>MergeOrUpload</c> so the whole
/// run is idempotent (safe to re-run during rehearsal). Real Azure only, fail loud (P1);
/// <see cref="DefaultAzureCredential"/> for auth (P6, no keys); <see cref="CancellationToken"/>
/// plumbed through every call (P7). Logs ids + counts only — never document bodies or PII.
/// </summary>
public sealed class SeedRunner(AzureSeedOptions options, ILogger<SeedRunner> logger)
{
    public async Task<int> SeedAsync(IReadOnlyList<CorpusDoc> docs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(docs);
        options.ValidateForSeeding();

        var credential = new DefaultAzureCredential();
        var searchEndpoint = new Uri(options.SearchEndpoint!);
        var openAiEndpoint = new Uri(options.OpenAiEndpoint!);

        // 1) Index schema (idempotent create-or-update).
        var indexClient = new SearchIndexClient(searchEndpoint, credential);
        var index = PrismSearchIndex.Build(options);
        logger.LogInformation(
            "Creating or updating index '{Index}' ({Fields} fields, {Dims}-dim vector, deployment '{Deployment}').",
            options.SearchIndex, index.Fields.Count, options.EmbeddingDimensions, options.EmbeddingDeployment);
        await indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: ct).ConfigureAwait(false);

        // 2) Embed each document's content with the SAME deployment the index vectorizer points to.
        var embeddingClient = new AzureOpenAIClient(openAiEndpoint, credential)
            .GetEmbeddingClient(options.EmbeddingDeployment);
        var embeddingOptions = new EmbeddingGenerationOptions { Dimensions = options.EmbeddingDimensions };

        var indexDocs = new List<IndexDoc>(docs.Count);
        foreach (var doc in docs)
        {
            ct.ThrowIfCancellationRequested();
            var embedding = await embeddingClient
                .GenerateEmbeddingAsync(doc.Content, embeddingOptions, ct)
                .ConfigureAwait(false);
            indexDocs.Add(IndexDoc.From(doc, embedding.Value.ToFloats().ToArray()));
        }

        logger.LogInformation("Embedded {Count} documents; uploading (MergeOrUpload, idempotent by id).", indexDocs.Count);

        // 3) Upsert (idempotent). Fail loud if any document is rejected.
        var searchClient = new SearchClient(searchEndpoint, options.SearchIndex, credential);
        var response = await searchClient
            .MergeOrUploadDocumentsAsync(indexDocs, cancellationToken: ct)
            .ConfigureAwait(false);

        var failed = response.Value.Results.Where(r => !r.Succeeded).ToArray();
        foreach (var f in failed)
        {
            logger.LogError("Upload failed for '{Key}' (status {Status}): {Error}", f.Key, f.Status, f.ErrorMessage);
        }

        if (failed.Length > 0)
        {
            throw new InvalidOperationException($"{failed.Length} of {indexDocs.Count} documents failed to upload.");
        }

        logger.LogInformation("Seed complete: {Count} documents indexed in '{Index}'.", indexDocs.Count, options.SearchIndex);
        return indexDocs.Count;
    }
}
