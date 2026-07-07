using System.Text.Json.Serialization;
using Prism.SeedData.Model;

namespace Prism.SeedData.Search;

/// <summary>
/// The subset of a <see cref="CorpusDoc"/> that is projected into the <c>prism-ratings</c> index,
/// plus the embedding vector. Field names match the index schema exactly (via
/// <see cref="JsonPropertyNameAttribute"/>) so uploads round-trip regardless of serializer policy.
/// </summary>
public sealed class IndexDoc
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("docType")] public string DocType { get; init; } = "";
    [JsonPropertyName("issuerId")] public string IssuerId { get; init; } = "";
    [JsonPropertyName("provider")] public string Provider { get; init; } = "";
    [JsonPropertyName("letter")] public string Letter { get; init; } = "";
    [JsonPropertyName("notch")] public int Notch { get; init; }
    [JsonPropertyName("dataClass")] public string DataClass { get; init; } = "";
    [JsonPropertyName("asOfDate")] public DateTimeOffset AsOfDate { get; init; }
    [JsonPropertyName("inputAsOfDate")] public DateTimeOffset InputAsOfDate { get; init; }
    // Issuer-only metadata + filing boundary (pkg-08 index extension). Empty/null on ratingCard rows.
    [JsonPropertyName("legalName")] public string LegalName { get; init; } = "";
    [JsonPropertyName("ticker")] public string Ticker { get; init; } = "";
    [JsonPropertyName("cik")] public string Cik { get; init; } = "";
    [JsonPropertyName("sector")] public string Sector { get; init; } = "";
    [JsonPropertyName("sampleBondIsin")] public string SampleBondIsin { get; init; } = "";
    [JsonPropertyName("latestFilingDate")] public DateTimeOffset? LatestFilingDate { get; init; }
    [JsonPropertyName("filingType")] public string FilingType { get; init; } = "";
    [JsonPropertyName("content")] public string Content { get; init; } = "";
    [JsonPropertyName("contentVector")] public IReadOnlyList<float> ContentVector { get; init; } = [];

    public static IndexDoc From(CorpusDoc doc, IReadOnlyList<float> vector) => new()
    {
        Id = doc.Id,
        DocType = doc.DocType,
        IssuerId = doc.IssuerId,
        Provider = doc.Provider,
        Letter = doc.Letter,
        Notch = doc.Notch,
        DataClass = doc.DataClass,
        AsOfDate = doc.AsOfDate,
        InputAsOfDate = doc.InputAsOfDate,
        LegalName = doc.LegalName ?? "",
        Ticker = doc.Ticker ?? "",
        Cik = doc.Cik ?? "",
        Sector = doc.Sector ?? "",
        SampleBondIsin = doc.SampleBondIsin ?? "",
        LatestFilingDate = doc.LatestFilingDate,
        FilingType = doc.FilingType ?? "",
        Content = doc.Content,
        ContentVector = vector,
    };
}
