namespace Prism.SeedData.Model;

/// <summary>One weighted rating factor with its sub-score and citation reference (mirrors the corpus JSON).</summary>
public sealed record CorpusFactor(string Name, decimal Weight, decimal Score, string SourceRef);

/// <summary>
/// A single corpus document as authored under <c>corpus/</c>. One record shape covers all doc types
/// (<c>issuer</c>, <c>ratingCard</c>, <c>methodology</c>, <c>reference</c>); type-specific fields are
/// nullable and validated per <c>docType</c> by <see cref="Corpus.CorpusValidator"/>. Deserialized
/// with <c>JsonSerializerDefaults.Web</c> (camelCase, case-insensitive); <c>required</c> members fail
/// loud (P1) when a mandatory field is absent.
/// </summary>
public sealed record CorpusDoc
{
    public required string Id { get; init; }
    public required string DocType { get; init; }
    public string IssuerId { get; init; } = "";
    public string Provider { get; init; } = "";
    public string Letter { get; init; } = "";
    public int Notch { get; init; }
    public required string DataClass { get; init; }
    public DateTimeOffset AsOfDate { get; init; }
    public DateTimeOffset InputAsOfDate { get; init; }
    public IReadOnlyList<CorpusFactor>? Factors { get; init; }
    public required string Content { get; init; }
    public string Disclaimer { get; init; } = "";

    // Issuer-only grounding metadata (kept in the model for validation; not all are indexed fields).
    public string? LegalName { get; init; }
    public string? Ticker { get; init; }
    public string? Cik { get; init; }
    public string? Sector { get; init; }
    public string? SampleBondIsin { get; init; }
    public DateTimeOffset? LatestFilingDate { get; init; }
    public string? FilingType { get; init; }
}
