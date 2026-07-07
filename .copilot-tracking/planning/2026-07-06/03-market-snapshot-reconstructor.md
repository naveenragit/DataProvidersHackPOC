# Plan 03 — Market Snapshot Reconstructor (as-of, deterministic C#)

**Objective:** Rebuild the market picture **as it was on the decision date** (no hindsight) — the
"public" lane. Deterministic C# over free real feeds (FRED / US Treasury FiscalData) filtered to the
observation date, plus publication-stamped synthetic news. **No LLM in the filtering path.**

**Depends on:** Plan 01. **Primary day:** 1.

> Honest framing (⚠ risk register): narrate this as **observation-date filtering** ("what was
> published by then"), never "point-in-time vintage data." A finance-literate judge will poke an
> overclaim.

---

## 1. Market data sources (free, real)

- [ ] FRED series client (`FRED_API_KEY`): pull DGS2, DGS10, T10Y2Y, and ICE BofA OAS credit-spread
      indices; each observation carries its own date
- [ ] US Treasury FiscalData client (no key): daily par yield curves
- [ ] Both via the typed resilient `HttpClient` from Plan 01 (retry-with-jitter, timeout, breaker)
- [ ] Cache pulled series (in-memory + optional Cosmos/Blob) so the demo doesn't hammer the APIs live

## 2. As-of filtering service (deterministic)

- [ ] `backend/FinancialServices.Api/Services/MarketSnapshotService.cs` (or `Analysis/`): given a
      decision date, return only observations **with observation date ≤ decision date**
- [ ] Rebuild the "tape" for the decision date: 2y/10y yields, 10y-2y spread, relevant credit spread —
      as they stood on that date
- [ ] Pure, testable, no LLM. Model the output as a `MarketSnapshot` record (record + `decimal` values)
- [ ] Unit tests asserting a later observation is excluded from an earlier decision date's snapshot

## 3. Publication-stamped synthetic news

- [ ] Author a small synthetic news set with explicit `publishedTs` (e.g., the NordStar downgrade
      article published the day after the decision) under `backend/FinancialServices.Api/data/news/`
- [ ] Filter news the same way — only items `publishedTs ≤ decision date` are "what the market knew"
- [ ] Label synthetic news clearly (no scraped Bloomberg/Terminal data — legally off-limits)
- [ ] The downgrade article that lands *after* the decision is the public-lane counterpart to the
      private-lane needle (the information gap between lanes)

## 4. MCP-shaped tool wrapper (judge talking point)

- [ ] Expose the market-data reconstruction as an **MCP-shaped tool** interface so the architecture
      mirrors a real future Bloomberg/MCP integration (Bloomberg is an MCP adopter). Keep it a thin
      wrapper over the deterministic service.

## 5. Integration with the pipeline

- [ ] Register `MarketSnapshotService` in DI (scoped)
- [ ] It will be invoked by the **Market Snapshot Reconstructor** function-tool in Plan 04 (agent =
      `gpt-4.1-mini`, but the filtering is deterministic C#; the model only narrates the tape)
- [ ] Emit the snapshot in the shape the Timeline Assembler (Plan 05) expects for the public lane

## Acceptance criteria

- Given a decision date, the service returns yields/spreads/news **strictly** ≤ that date
- Unit tests prove hindsight exclusion (later observations never leak into an earlier snapshot)
- FRED/Treasury calls are resilient and cached; no key material in code
- The post-decision downgrade article is correctly excluded from the as-of snapshot and available for
  the "gap between lanes" narrative

## Cut-lines

- If live FRED/Treasury access is flaky during the demo, serve from the committed cache captured
  earlier (real data, snapshotted — not synthetic) so the tape stays real
- Synthetic news can be trimmed to just the one downgrade article + a couple of neutral items
