using Azure.Search.Documents.Indexes.Models;
using Prism.SeedData.Configuration;

namespace Prism.SeedData.Search;

/// <summary>
/// Builds the <c>prism-ratings</c> <see cref="SearchIndex"/> (implementationPlan/03 §C, architecturalPlan/08):
/// filterable/sortable as-of metadata, a machine-readable <c>dataClass</c> label (P1), a 1536-dim
/// <c>contentVector</c> with an HNSW profile, an Azure OpenAI vectorizer pinned to the SAME embedding
/// deployment used at index time (so query-time integrated vectorization matches — HACKATHON-FINDINGS §6),
/// and a semantic configuration for hybrid + semantic ranking.
/// </summary>
public static class PrismSearchIndex
{
    public const string HnswConfigName = "prism-hnsw";
    public const string VectorProfileName = "prism-vector-profile";
    public const string VectorizerName = "prism-openai-vectorizer";
    public const string SemanticConfigName = "prism-semantic";
    public const string ContentVectorField = "contentVector";
    public const string ContentField = "content";

    public static SearchIndex Build(AzureSeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SearchIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OpenAiEndpoint);

        var fields = new List<SearchField>
        {
            new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
            new SimpleField("docType", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("issuerId", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("provider", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("letter", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("notch", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
            new SimpleField("dataClass", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("asOfDate", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
            new SimpleField("inputAsOfDate", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
            // Issuer-only metadata + filing boundary (pkg 08): retrievable so the API can serve the
            // issuer cast (GET /issuers) and build the stale-input boundary from real Search. Empty on
            // ratingCard rows. Additive — a re-seed populates them; older docs simply carry nulls.
            new SimpleField("legalName", SearchFieldDataType.String),
            new SimpleField("ticker", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("cik", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("sector", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("sampleBondIsin", SearchFieldDataType.String),
            new SimpleField("latestFilingDate", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
            new SimpleField("filingType", SearchFieldDataType.String) { IsFilterable = true },
            new SearchableField(ContentField),
            new SearchField(ContentVectorField, SearchFieldDataType.Collection(SearchFieldDataType.Single))
            {
                IsSearchable = true,
                VectorSearchDimensions = options.EmbeddingDimensions,
                VectorSearchProfileName = VectorProfileName,
            },
        };

        var vectorSearch = new VectorSearch
        {
            Algorithms = { new HnswAlgorithmConfiguration(HnswConfigName) },
            Profiles =
            {
                new VectorSearchProfile(VectorProfileName, HnswConfigName) { VectorizerName = VectorizerName },
            },
            Vectorizers =
            {
                new AzureOpenAIVectorizer(VectorizerName)
                {
                    Parameters = new AzureOpenAIVectorizerParameters
                    {
                        ResourceUri = new Uri(options.OpenAiEndpoint),
                        DeploymentName = options.EmbeddingDeployment,
                        ModelName = options.EmbeddingModel,
                    },
                },
            },
        };

        var semanticSearch = new SemanticSearch
        {
            DefaultConfigurationName = SemanticConfigName,
            Configurations =
            {
                new SemanticConfiguration(
                    SemanticConfigName,
                    new SemanticPrioritizedFields
                    {
                        ContentFields = { new SemanticField(ContentField) },
                        KeywordsFields = { new SemanticField("issuerId") },
                    }),
            },
        };

        return new SearchIndex(options.SearchIndex)
        {
            Fields = fields,
            VectorSearch = vectorSearch,
            SemanticSearch = semanticSearch,
        };
    }
}
