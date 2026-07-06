# 04 — Agent Architecture

How every agent is created, grounded, and kept honest. Read before touching anything in `Agents/` or
`Orchestration/`.

---

## The golden split (P2)

| Concern | Where | May use LLM? |
|---|---|---|
| Notch math, gap ordering, attribution, flag triggers, confidence | `Analysis/` engines | **No** |
| Retrieval, explanation, narration, orchestration planning | `Agents/` + `Orchestration/` | Yes |

Agents **consume** deterministic outputs and **narrate/cite** them. They must never recompute or
override a number the engines produced.

## Foundry agent standards

- Agents are **Azure AI Foundry Prompt Agents** on the **Responses API v2** (`Azure.AI.Projects` 2.x +
  `Microsoft.Agents.AI[.AzureAI]`). **Do not** use the v1 assistants/threads/runs API. Do not mix SDK
  generations (HACKATHON-FINDINGS §4).
- Agents are **defined in Foundry** and **referenced by name** in C# — never recreated per request.
- Auth is Entra ID only on the Projects client (`DefaultAzureCredential`), no API key.
- Verify `GetAIAgent` / `RunAsync` / response type against the installed package version before coding
  (accelerator caveat).

## C# wrapper pattern (every agent looks like this)

```csharp
public sealed class ProviderExplainerAgent(
    IOptions<AzureOptions> options, ISearchCorpus corpus, ILogger<ProviderExplainerAgent> logger)
{
    private static readonly ActivitySource Activity = new("FinancialServices.Agents");

    public async Task<ProviderExplanation> ExplainAsync(string issuerId, Provider provider, CancellationToken ct)
    {
        using var span = Activity.StartActivity("ProviderExplainerAgent.Explain");   // OTel — always
        var context = await corpus.GetRatingContextAsync(issuerId, provider, ct);    // grounding in
        var agent = await Project(options).GetAIAgentAsync("ProviderExplainerAgent", cancellationToken: ct);
        var response = await agent.RunAsync(BuildGroundedPrompt(provider, context), cancellationToken: ct);
        return Parse(response.Text, context);   // preserve citations → sourceRefs
    }
}
```

Rules: scoped DI registration; `ActivitySource("FinancialServices.Agents")` span on every call;
`CancellationToken` plumbed; grounding retrieved in code and passed in (do not let the model free-roam).

## Agent taxonomy

| Agent | Role | Model | Grounding |
|---|---|---|---|
| `ReconciliationOrchestrator` | plan sweep, own scope gate, assemble dossier | `gpt-5` medium | function tools |
| `ProviderExplainerAgent` | explain one provider's assessment in its terms | `gpt-4.1-mini` | AI Search (filtered) |
| `FundamentalsAgent` | state real EDGAR/FRED inputs + as-of dates | `gpt-4.1-mini` | connectors |
| `DivergenceNarratorAgent` | narrate each waterfall bucket | `gpt-5-mini` | `PairDivergence` |
| `RedFlagNarratorAgent` | narrate each flag, cite evidence | `gpt-5-mini` | `RedFlag` objects |

Models are configurable via `Prism__Models__*` — never hardcode a deployment name in an agent.

## Grounding & citation contract

- Retrieve context in code (AI Search filtered by `issuerId`+`provider`, or connector output). Instruct
  the agent to answer **only** from supplied context and to return doc-id citations.
- Every narration must reference its `EvidenceRefs`/`sourceRef`. Post-validate; if a citation is
  missing or a number contradicts the deterministic value, **discard the narration** and show the raw
  deterministic rule (P2).

## Orchestration (AG-UI)

- Expose **one** AG-UI agent (`ReconciliationOrchestrator`) via `MapAGUI("/prism", …)`. Specialists +
  deterministic engines are registered as `AIFunctionFactory.Create(...)` **function tools** with
  `[Description]`; each tool call streams to the UI as a generative card.
- The scope gate is an `ApprovalRequiredAIFunction` → CopilotKit `renderAndWaitForResponse`.
- **Do not** rely on prerelease .NET multi-agent *workflow* streaming (Python-only). Keep the
  synchronous REST + SSE fallback (`POST /api/v1/reconciliations`) buildable at all times.

## Safety

- Content Safety on any user-supplied free text before it reaches a model.
- Never pass PII/secrets into prompts. Treat tool arguments from the model as hostile — re-authorize
  and re-validate in the API (see [06](06-security-and-compliance.md)).
