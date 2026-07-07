using Azure.Search.Documents.Indexes.Models;
using FluentAssertions;
using Prism.SeedData.Configuration;
using Prism.SeedData.Search;
using Xunit;

namespace Prism.SeedData.Tests;

/// <summary>
/// Offline checks of the <c>prism-ratings</c> index schema (no Azure network): the key field, the
/// 1536-dim vector field, the Azure OpenAI vectorizer pinned to the configured deployment (so
/// query-time integrated vectorization matches index time), the semantic configuration, and the
/// filterable/sortable as-of metadata.
/// </summary>
public sealed class PrismSearchIndexTests
{
    private static AzureSeedOptions Options() => new()
    {
        SearchEndpoint = "https://example.search.windows.net",
        SearchIndex = "prism-ratings",
        OpenAiEndpoint = "https://example.openai.azure.com",
        EmbeddingDeployment = "text-embedding-3-small",
        EmbeddingModel = "text-embedding-3-small",
        EmbeddingDimensions = 1536,
    };

    [Fact]
    public void Index_has_id_key_and_1536_dim_vector_field()
    {
        var index = PrismSearchIndex.Build(Options());

        index.Name.Should().Be("prism-ratings");
        index.Fields.Single(f => f.IsKey == true).Name.Should().Be("id");

        var vector = index.Fields.Single(f => f.Name == PrismSearchIndex.ContentVectorField);
        vector.VectorSearchDimensions.Should().Be(1536);
        vector.VectorSearchProfileName.Should().Be(PrismSearchIndex.VectorProfileName);
    }

    [Fact]
    public void Index_vectorizer_points_at_the_configured_deployment_and_model()
    {
        var index = PrismSearchIndex.Build(Options());

        index.VectorSearch.Should().NotBeNull();
        index.VectorSearch!.Vectorizers.Should().ContainSingle();
        var vectorizer = index.VectorSearch.Vectorizers.OfType<AzureOpenAIVectorizer>().Single();
        vectorizer.Parameters!.DeploymentName.Should().Be("text-embedding-3-small");
        $"{vectorizer.Parameters.ModelName}".Should().Be("text-embedding-3-small");
    }

    [Fact]
    public void Index_has_the_expected_semantic_configuration()
    {
        var index = PrismSearchIndex.Build(Options());

        index.SemanticSearch.Should().NotBeNull();
        index.SemanticSearch!.Configurations.Should().Contain(c => c.Name == PrismSearchIndex.SemanticConfigName);
    }

    [Theory]
    [InlineData("asOfDate")]
    [InlineData("inputAsOfDate")]
    public void As_of_metadata_fields_are_filterable_and_sortable(string fieldName)
    {
        var index = PrismSearchIndex.Build(Options());

        var field = index.Fields.Single(f => f.Name == fieldName);
        field.IsFilterable.Should().BeTrue();
        field.IsSortable.Should().BeTrue();
    }

    [Theory]
    [InlineData("docType")]
    [InlineData("issuerId")]
    [InlineData("provider")]
    [InlineData("dataClass")]
    public void Facet_and_filter_fields_are_filterable(string fieldName)
    {
        var index = PrismSearchIndex.Build(Options());

        index.Fields.Single(f => f.Name == fieldName).IsFilterable.Should().BeTrue();
    }
}
