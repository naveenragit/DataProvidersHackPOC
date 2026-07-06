# Azure Financial Services Skill

Domain-specific knowledge and implementation patterns for Azure Data and AI Services
in regulated financial services applications (Capital Markets, Banking, Insurance).

The service layer is **C# / ASP.NET Core (.NET 9)**; agents use the **Microsoft Agent
Framework (.NET)** over **Azure AI Foundry**.

## When to Use This Skill

Load this skill when:
- Selecting the right Azure service for a financial use case
- Implementing Azure `.NET` SDK integrations (authentication, client setup, error handling)
- Designing Cosmos DB schemas for financial data
- Setting up Azure AI Search for financial document retrieval
- Implementing Azure AI Foundry multi-agent pipelines for financial workflows
- Ensuring compliance with financial regulations using Azure services

---

## Azure AI Foundry — Agent Patterns for Finance

### Capital Markets Agent Pipeline

Run independent agents concurrently with `Task.WhenAll`, then merge into a briefing.

```csharp
public async Task<PreMeetingBriefing> RunPreMeetingPrepAsync(
    string clientId, string meetingId, CancellationToken ct)
{
    // NewsAgent and AdvisoryAgent run concurrently
    var newsTask = _foundry.RunAgentAsync("MarketIntelligenceAgent", clientId, ct);
    var advisoryTask = _foundry.RunAgentAsync("AdvisoryAgent", clientId, ct);

    await Task.WhenAll(newsTask, advisoryTask);

    return new PreMeetingBriefing(
        NewsSummary: newsTask.Result,
        AdvisoryInsights: advisoryTask.Result,
        ClientId: clientId,
        MeetingId: meetingId);
}
```

### Agent Model Assignments

Recommended model selection for financial agents:

| Agent Type | Model | Reason |
|---|---|---|
| Transcription / Extraction | `gpt-4o` | Accuracy on financial names and terminology |
| Portfolio Analysis | `gpt-4.1` | Deeper quantitative reasoning |
| Recommendation | `o4-mini` | Fast iterative recommendations |
| Advisory / Research | `o4-mini` with grounding | Current market data |
| Tax / Compliance | `o4-mini` | Regulatory interpretation |
| Risk Advisory | `gpt-4.1` | Complex risk narrative |
| Portfolio Construction | `o1` | Mathematical optimization |

### Grounding for Market Data

Grounding/tool connections are configured on the agent in Azure AI Foundry and referenced by
connection id. The `.NET` agent picks them up when you invoke the named agent.

```csharp
// Connection id comes from bound options; the agent is defined with the grounding tool in Foundry.
AIAgent marketAgent = await projectClient
    .GetAIAgentAsync("MarketIntelligenceAgent", cancellationToken: ct);
AgentRunResponse response = await marketAgent.RunAsync(
    "Summarize market-moving news for the client's holdings.", cancellationToken: ct);
```

---

## Cosmos DB — Financial Data Schemas

Documents use camelCase properties (configure `CosmosPropertyNamingPolicy.CamelCase`).

### Client Profile Container (`clients`, pk: `/advisorId`)
```json
{
  "id": "cli_001",
  "advisorId": "adv_001",
  "name": "John Smith",
  "riskProfile": "moderate",
  "goals": ["retirement_2035", "college_2028"],
  "lifeEvents": ["marriage_2023"],
  "assetMentions": ["AAPL", "MSFT"],
  "kycStatus": "verified",
  "kycLastVerified": "2024-01-15T00:00:00Z",
  "createdAt": "2023-01-01T00:00:00Z",
  "updatedAt": "2024-01-15T00:00:00Z"
}
```

### Session/Meeting Container (`sessions`, pk: `/clientId`)
```json
{
  "id": "sess_001",
  "clientId": "cli_001",
  "advisorId": "adv_001",
  "startTime": "2024-01-15T10:00:00Z",
  "endTime": "2024-01-15T11:00:00Z",
  "transcript": [],
  "recommendations": [],
  "status": "completed",
  "gate1Approved": true,
  "gate1ApprovedBy": "adv_001",
  "gate1ApprovedAt": "2024-01-15T11:15:00Z"
}
```

### Portfolio Container (`portfolios`, pk: `/clientId`)
```json
{
  "id": "pf_001",
  "clientId": "cli_001",
  "advisorId": "adv_001",
  "totalValue": 1250000.00,
  "currency": "USD",
  "benchmark": "SPY",
  "positions": [
    {
      "instrumentId": "US0378331005",
      "ticker": "AAPL",
      "assetClass": "equity",
      "quantity": 100,
      "marketValue": 18500.00,
      "weight": 0.0148,
      "costBasis": 15000.00
    }
  ],
  "riskScore": 62.5,
  "lastRebalanced": "2024-01-01T00:00:00Z",
  "updatedAt": "2024-01-15T00:00:00Z"
}
```

Use `decimal` (never `double`) when mapping monetary values into C# records.

---

## Azure AI Search — Financial Document Indexes

### Three standard indexes for financial platforms

| Index | Content | Key Fields |
|---|---|---|
| `cmclients` | Client documents, meeting notes, KYC records | `advisorId`, `clientId`, `documentType` |
| `cmmeetings` | Meeting transcripts, summaries, action items | `sessionId`, `clientId`, `meetingDate` |
| `cmdocuments` | Research reports, prospectuses, annual reports, earnings | `instrumentId`, `documentType`, `documentDate` |

### Hybrid Search for Financial Research

```csharp
public async Task<IReadOnlyList<SearchDocument>> SearchFinancialResearchAsync(
    string query, string? instrumentId, string? documentType, int top, CancellationToken ct)
{
    var filters = new List<string>();
    if (instrumentId is not null)
        filters.Add(SearchFilter.Create($"instrumentId eq {instrumentId}"));
    if (documentType is not null)
        filters.Add(SearchFilter.Create($"documentType eq {documentType}"));

    var client = new SearchClient(
        new Uri(_options.SearchEndpoint), "cmdocuments", new DefaultAzureCredential());

    var options = new SearchOptions
    {
        Size = top,
        Filter = filters.Count > 0 ? string.Join(" and ", filters) : null,
        VectorSearch = new()
        {
            Queries = { new VectorizableTextQuery(query) { KNearestNeighborsCount = 50, Fields = { "contentVector" } } }
        },
    };
    foreach (var f in new[] { "id", "title", "content", "instrumentId", "documentType", "documentDate", "sourceUrl" })
        options.Select.Add(f);

    SearchResults<SearchDocument> results = await client.SearchAsync<SearchDocument>(query, options, ct);
    var docs = new List<SearchDocument>();
    await foreach (var r in results.GetResultsAsync()) docs.Add(r.Document);
    return docs;
}
```

`SearchFilter.Create($"...")` parameterizes interpolated values to prevent filter injection.

---

## Azure Speech — Financial Meeting Transcription

### Optimizing for Financial Terminology

```csharp
public SpeechConfig CreateSpeechConfigForFinance()
{
    var config = SpeechConfig.FromSubscription(_options.SpeechKey, _options.SpeechRegion);
    config.SpeechRecognitionLanguage = "en-US";
    // Reduce end-of-sentence silence detection for meeting scenarios
    config.SetProperty(PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, "2000");
    // Word-level timestamps for compliance
    config.RequestWordLevelTimestamps();
    return config;
}
```

### Financial Phrase List (improve recognition accuracy)

```csharp
var phraseList = PhraseListGrammar.FromRecognizer(recognizer);
foreach (var phrase in new[]
{
    "EBITDA", "VaR", "alpha generation", "basis points", "rebalancing",
    "KYC", "AML", "MiFID", "ESG", "Sharpe ratio"
})
{
    phraseList.AddPhrase(phrase);
}
```

---

## Azure Content Safety — Financial Platform Setup

Financial platforms have a lower tolerance for harmful content given the regulatory environment.

```csharp
public async Task<ContentSafetyResult> CheckAsync(
    string text, string context, CancellationToken ct) // context: "advisory" | "research" | "client_input"
{
    var client = new ContentSafetyClient(
        new Uri(_options.ContentSafetyEndpoint), new DefaultAzureCredential());

    AnalyzeTextResult result = await client.AnalyzeTextAsync(new AnalyzeTextOptions(text), ct);

    // Financial platforms: flag severity >= 2 (not just >= 4)
    var flags = result.CategoriesAnalysis
        .Where(c => c.Severity >= 2)
        .Select(c => new SafetyFlag(c.Category.ToString(), c.Severity ?? 0))
        .ToList();

    return new ContentSafetyResult(Safe: flags.Count == 0, Flags: flags, Context: context);
}
```

---

## Azure Monitor — Financial Audit Telemetry

Capture financial platform events as custom events/metrics. Never include PII values — only
IDs and operation metadata.

```csharp
public sealed class FinancialEventLogger(TelemetryClient telemetry)
{
    public void LogFinancialEvent(
        string eventName, string advisorId, string clientId, string sessionId,
        IReadOnlyDictionary<string, object> metadata)
    {
        var props = new Dictionary<string, string>
        {
            ["advisorId"] = advisorId,
            ["clientId"] = clientId,
            ["sessionId"] = sessionId,
        };
        foreach (var (k, v) in metadata) props[k] = v.ToString() ?? string.Empty;

        telemetry.TrackEvent(eventName, props);
    }
}

// Usage:
// logger.LogFinancialEvent("recommendation_approved", "adv_001", "cli_001", "sess_001",
//     new Dictionary<string, object> { ["recommendationCount"] = 3, ["gate"] = "GATE-1" });
```
