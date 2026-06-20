# Azure Financial Services Skill

Domain-specific knowledge and implementation patterns for Azure Data and AI Services
in regulated financial services applications (Capital Markets, Banking, Insurance).

## When to Use This Skill

Load this skill when:
- Selecting the right Azure service for a financial use case
- Implementing Azure SDK integrations (authentication, client setup, error handling)
- Designing Cosmos DB schemas for financial data
- Setting up Azure AI Search for financial document retrieval
- Implementing Azure AI Foundry multi-agent pipelines for financial workflows
- Ensuring compliance with financial regulations using Azure services

---

## Azure AI Foundry — Agent Patterns for Finance

### Capital Markets Agent Pipeline
```python
# Parallel pre-meeting research: news + advisory analysis
async def run_pre_meeting_prep(client_id: str, meeting_id: str) -> dict:
    """Run NewsAgent and AdvisoryAgent concurrently, merge into briefing."""
    async with AIProjectClient(
        endpoint=settings.azure_ai_project_endpoint,
        credential=DefaultAzureCredential(),
    ) as client:
        news_thread = await client.agents.create_thread()
        advisory_thread = await client.agents.create_thread()

        # Run agents concurrently
        news_task = run_agent_thread(client, "NewsAgent", news_thread.id, client_id)
        advisory_task = run_agent_thread(client, "AdvisoryAgent", advisory_thread.id, client_id)
        news_result, advisory_result = await asyncio.gather(news_task, advisory_task)

        return {
            "news_summary": news_result,
            "advisory_insights": advisory_result,
            "client_id": client_id,
            "meeting_id": meeting_id,
        }
```

### Agent Model Assignments
Recommended model selection for financial agents:

| Agent Type | Model | Reason |
|---|---|---|
| Transcription / Extraction | `gpt-4o` | Accuracy on financial names and terminology |
| Portfolio Analysis | `gpt-4.1` | Deeper quantitative reasoning |
| Recommendation | `o4-mini` | Fast iterative recommendations |
| Advisory / Research | `o4-mini` with Bing Grounding | Current market data |
| Tax / Compliance | `o4-mini` | Regulatory interpretation |
| Risk Advisory | `gpt-4.1` | Complex risk narrative |
| Portfolio Construction | `o1` | Mathematical optimization |

### Bing Grounding for Market Data
```python
from azure.ai.projects.models import BingGroundingTool

bing_tool = BingGroundingTool(
    connection_id=settings.bing_connection_id
)
agent = await client.agents.create_agent(
    model="o4-mini",
    name="MarketIntelligenceAgent",
    instructions="You analyze financial markets...",
    tools=bing_tool.definitions,
)
```

---

## Cosmos DB — Financial Data Schemas

### Client Profile Container (`clients`, pk: `/advisor_id`)
```json
{
  "id": "cli_001",
  "advisor_id": "adv_001",
  "name": "John Smith",
  "risk_profile": "moderate",
  "goals": ["retirement_2035", "college_2028"],
  "life_events": ["marriage_2023"],
  "asset_mentions": ["AAPL", "MSFT"],
  "estate_notes": "...",
  "kyc_status": "verified",
  "kyc_last_verified": "2024-01-15T00:00:00Z",
  "created_at": "2023-01-01T00:00:00Z",
  "updated_at": "2024-01-15T00:00:00Z"
}
```

### Session/Meeting Container (`sessions`, pk: `/client_id`)
```json
{
  "id": "sess_001",
  "client_id": "cli_001",
  "advisor_id": "adv_001",
  "start_time": "2024-01-15T10:00:00Z",
  "end_time": "2024-01-15T11:00:00Z",
  "transcript": [...],
  "sentiment_data": {...},
  "recommendations": [...],
  "summaries": {...},
  "status": "completed",
  "gate_1_approved": true,
  "gate_1_approved_by": "adv_001",
  "gate_1_approved_at": "2024-01-15T11:15:00Z"
}
```

### Portfolio Container (`portfolios`, pk: `/client_id`)
```json
{
  "id": "pf_001",
  "client_id": "cli_001",
  "advisor_id": "adv_001",
  "total_value": 1250000.00,
  "currency": "USD",
  "benchmark": "SPY",
  "positions": [
    {
      "instrument_id": "US0378331005",
      "ticker": "AAPL",
      "asset_class": "equity",
      "quantity": 100,
      "market_value": 18500.00,
      "weight": 0.0148,
      "cost_basis": 15000.00
    }
  ],
  "risk_score": 62.5,
  "last_rebalanced": "2024-01-01T00:00:00Z",
  "updated_at": "2024-01-15T00:00:00Z"
}
```

---

## Azure AI Search — Financial Document Indexes

### Three standard indexes for financial platforms

| Index | Content | Key Fields |
|---|---|---|
| `cmclients` | Client documents, meeting notes, KYC records | `advisor_id`, `client_id`, `document_type` |
| `cmmeetings` | Meeting transcripts, summaries, action items | `session_id`, `client_id`, `meeting_date` |
| `cmdocuments` | Research reports, prospectuses, annual reports, earnings | `instrument_id`, `document_type`, `document_date` |

### Hybrid Search for Financial Research
```python
async def search_financial_research(
    query: str,
    instrument_id: str | None = None,
    document_type: str | None = None,
    top: int = 5,
) -> list[dict]:
    filter_parts = []
    if instrument_id:
        filter_parts.append(f"instrument_id eq '{instrument_id}'")
    if document_type:
        filter_parts.append(f"document_type eq '{document_type}'")

    client = SearchClient(
        endpoint=settings.azure_search_endpoint,
        index_name="cmdocuments",
        credential=DefaultAzureCredential(),
    )
    async with client:
        results = await client.search(
            search_text=query,
            filter=" and ".join(filter_parts) if filter_parts else None,
            vector_queries=[
                VectorizableTextQuery(text=query, k_nearest_neighbors=50, fields="content_vector")
            ],
            top=top,
            select=["id", "title", "content", "instrument_id", "document_type", "document_date", "source_url"],
        )
        return [r async for r in results]
```

---

## Azure Speech — Financial Meeting Transcription

### Optimizing for Financial Terminology
```python
def create_speech_config_for_finance() -> speechsdk.SpeechConfig:
    config = speechsdk.SpeechConfig(
        subscription=settings.azure_speech_key,
        region=settings.azure_speech_region,
    )
    config.speech_recognition_language = "en-US"
    # Reduce end-of-sentence silence detection for meeting scenarios
    config.set_property(speechsdk.PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, "2000")
    # Enable word-level timestamps for compliance
    config.request_word_level_timestamps()
    # Enable speaker diarization
    config.set_property(speechsdk.PropertyId.SpeechServiceConnection_RecognizedSpeechDuration, "true")
    return config
```

### Financial Phrase List (improve recognition accuracy)
```python
phrase_list = speechsdk.PhraseListGrammar.from_recognizer(recognizer)
phrase_list.addPhrase("EBITDA")
phrase_list.addPhrase("VaR")
phrase_list.addPhrase("alpha generation")
phrase_list.addPhrase("basis points")
phrase_list.addPhrase("rebalancing")
phrase_list.addPhrase("KYC")
phrase_list.addPhrase("AML")
phrase_list.addPhrase("MiFID")
phrase_list.addPhrase("ESG")
phrase_list.addPhrase("Sharpe ratio")
```

---

## Azure Content Safety — Financial Platform Setup

Financial platforms have a lower tolerance for harmful content given the regulatory environment.

```python
async def run_financial_content_safety(
    text: str,
    context: str = "advisory",  # "advisory" | "research" | "client_input"
) -> dict:
    """
    Check text against content safety thresholds appropriate for financial context.
    Returns dict with `safe: bool` and `flags: list`.
    """
    client = ContentSafetyClient(
        endpoint=settings.content_safety_endpoint,
        credential=DefaultAzureCredential(),
    )
    async with client:
        request = AnalyzeTextOptions(text=text)
        response = await client.analyze_text(request)

        flags = []
        for item in response.categories_analysis:
            # Financial platforms: flag severity >= 2 (not just >= 4)
            if item.severity and item.severity >= 2:
                flags.append({"category": item.category.value, "severity": item.severity})

        return {
            "safe": len(flags) == 0,
            "flags": flags,
            "text_length": len(text),
            "context": context,
        }
```

---

## Azure Monitor — Financial Audit Telemetry

All financial platform metrics should be captured as custom events in Application Insights.

```python
from applicationinsights import TelemetryClient

tc = TelemetryClient(settings.appinsights_instrumentation_key)

def log_financial_event(
    event_name: str,
    advisor_id: str,
    client_id: str,
    session_id: str,
    metadata: dict,
) -> None:
    """Log a financial operation event to Application Insights."""
    # Never include PII values — only IDs and operation metadata
    tc.track_event(
        event_name,
        properties={
            "advisor_id": advisor_id,
            "client_id": client_id,
            "session_id": session_id,
            "environment": settings.environment,
            **{k: str(v) for k, v in metadata.items()},
        },
    )
    tc.flush()

# Usage:
log_financial_event(
    "recommendation_approved",
    advisor_id="adv_001",
    client_id="cli_001",
    session_id="sess_001",
    metadata={"recommendation_count": 3, "gate": "GATE-1"},
)
```
