# 06 — Foundry Agents

**Purpose:** the LLM layer — Azure AI Foundry **Prompt Agents** (Responses API v2) that *retrieve,
narrate, and cite*. They never compute notch math or fire flags (that's package 05). Thin C#
wrappers follow the accelerator's `PortfolioAnalysisAgent` pattern.

**Depends on:** 03, 04. **Blocks:** 07.

> **SDK caveat (accelerator + HACKATHON-FINDINGS §4):** do **not** mix SDK generations. Use modern
> `Azure.AI.Projects` 2.x + `Microsoft.Agents.AI[.AzureAI]`, Responses API v2 — never v1
> assistants/threads/runs. Verify `GetAIAgent` / `RunAsync` / response type against the installed
> package version before writing all six.

---

## A. Agents to define in Foundry (referenced by name from C#, never recreated)

| Agent | Model (`Prism__Models__*`) | Grounding | Output |
|---|---|---|---|
| `ProviderExplainerAgent` (parametrized per provider) | `gpt-4.1-mini` (chatty, cheap) | Azure AI Search filtered to `issuerId`+`provider` | Cites the provider's rating card + methodology in that provider's terms |
| `FundamentalsAgent` | `gpt-4.1-mini` | EDGAR/FRED tool outputs (package 04) | States ground-truth inputs + as-of dates |
| `RedFlagNarratorAgent` | `gpt-5-mini` | Deterministic flag objects (package 05) | Narrates each flag; **must** cite both evidence refs; adds no new claims |
| `DivergenceNarratorAgent` | `gpt-5-mini` | `PairDivergence` buckets (package 05) | Fills each bucket `Explanation` with a citation |
| `ReconciliationOrchestrator` | `gpt-5` (medium reasoning) | function tools (package 07) | Plans sweep, owns HITL gate, assembles dossier |

Keep chatty agents on `gpt-4.1-mini` to avoid 429s; reserve `gpt-5` for the orchestrator.

---

## B. C# wrapper pattern

Follow `templates/csharp-api/FinancialServices.Api/Agents/PortfolioAnalysisAgent.cs`:

```csharp
public sealed class ProviderExplainerAgent(IOptions<AzureOptions> options, ISearchCorpus corpus,
    ILogger<ProviderExplainerAgent> logger)
{
    private static readonly ActivitySource Activity = new("FinancialServices.Agents");

    public async Task<ProviderExplanation> ExplainAsync(
        string issuerId, Provider provider, CancellationToken ct)
    {
        using var span = Activity.StartActivity("ProviderExplainerAgent.Explain");
        var cards = await corpus.GetRatingContextAsync(issuerId, provider, ct); // AI Search, filtered

        var project = new AIProjectClient(new Uri(options.Value.AiProjectEndpoint), new DefaultAzureCredential());
        AIAgent agent = await project.GetAIAgentAsync("ProviderExplainerAgent", cancellationToken: ct);

        var response = await agent.RunAsync(BuildPrompt(provider, cards), cancellationToken: ct);
        logger.LogInformation("ProviderExplainer {Provider} for {Issuer}", provider, issuerId);
        return Parse(response.Text, cards); // keep citations → SourceRefs
    }
}
```

- Enforce **grounded output**: the prompt instructs the agent to answer *only* from the supplied
  cards + methodology and to return citations (doc ids). Reject/flag ungrounded claims.
- Register all wrappers as scoped services in `ServiceCollectionExtensions.AddDomainServices`.
- Apply **Content Safety** on any free-text the user supplies (issuer notes) before it reaches a model.

---

## C. Narrator contract (keeps the LLM honest)

`RedFlagNarratorAgent` and `DivergenceNarratorAgent` receive the **already-computed** objects and
return only prose + citations. Post-validate: the narrative must reference every `EvidenceRef`; if it
invents a number that contradicts the deterministic value, drop the narrative and show the raw rule.

---

## Acceptance for this package
- [ ] Each provider agent returns a cited explanation for NordStar from **live Foundry**.
- [ ] `FundamentalsAgent` reports NordStar Q3 figures with the real filing date.
- [ ] `RedFlagNarratorAgent` narrates the STALE_INPUT flag citing both the MSCI card and the EDGAR date.
- [ ] Narrators never alter deterministic numbers (validation test).
- [ ] All agents run within TPM quota (chatty ones on `gpt-4.1-mini`).
