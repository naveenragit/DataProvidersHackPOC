using FinancialServices.Api.Infrastructure.Errors;

namespace Prism.SeedData.Configuration;

/// <summary>
/// Configuration for the corpus seeder, bound from the <c>Azure</c> section (architecturalPlan/05,
/// double-underscore env convention: <c>Azure__SearchEndpoint</c>, <c>Azure__EmbeddingDeployment</c>,
/// …). No secrets: authentication is <see cref="Azure.Identity.DefaultAzureCredential"/> (P6). Live
/// seeding fails loud (P1) naming any missing setting — it never invents a placeholder.
/// </summary>
public sealed class AzureSeedOptions
{
    /// <summary>Azure AI Search endpoint, e.g. <c>https://&lt;svc&gt;.search.windows.net</c>.</summary>
    public string? SearchEndpoint { get; init; }

    /// <summary>Target index name (<c>prism-ratings</c>).</summary>
    public string? SearchIndex { get; init; }

    /// <summary>Azure OpenAI endpoint used for embeddings, e.g. <c>https://&lt;acct&gt;.openai.azure.com</c>.</summary>
    public string? OpenAiEndpoint { get; init; }

    /// <summary>Deployment name of the embedding model (must match the index-time vectorizer).</summary>
    public string EmbeddingDeployment { get; init; } = "text-embedding-3-small";

    /// <summary>Underlying embedding model name (pinned to the deployment's model).</summary>
    public string EmbeddingModel { get; init; } = "text-embedding-3-small";

    /// <summary>
    /// Embedding dimensionality (1536). This drives BOTH the index field length and the client-side
    /// embedding request. Integrated (query-time) vectorization via the index vectorizer uses the
    /// model's DEFAULT output dimension, so this value must equal that default (1536 for
    /// text-embedding-3-small) or index-time and query-time vectors would silently differ in length
    /// (HACKATHON-FINDINGS §6). The verifier asserts the index field dimension == this value.
    /// </summary>
    public int EmbeddingDimensions { get; init; } = 1536;

    /// <summary>Throw a clear, actionable error (P1) if anything required for a live seed is unset.</summary>
    public void ValidateForSeeding()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(SearchEndpoint))
        {
            missing.Add("Azure__SearchEndpoint");
        }

        if (string.IsNullOrWhiteSpace(SearchIndex))
        {
            missing.Add("Azure__SearchIndex");
        }

        if (string.IsNullOrWhiteSpace(OpenAiEndpoint))
        {
            missing.Add("Azure__OpenAiEndpoint");
        }

        if (string.IsNullOrWhiteSpace(EmbeddingDeployment))
        {
            missing.Add("Azure__EmbeddingDeployment");
        }

        if (EmbeddingDimensions <= 0)
        {
            missing.Add("Azure__EmbeddingDimensions (> 0)");
        }

        if (missing.Count > 0)
        {
            throw new ConfigurationException(
                string.Join(", ", missing),
                "Missing required configuration for live seeding (P1 — set these before running 'seed'): "
                + string.Join(", ", missing)
                + ". Authentication uses DefaultAzureCredential (run 'az login'); no keys are read.");
        }
    }
}
