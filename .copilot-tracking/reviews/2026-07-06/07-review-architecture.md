# ⚔️ Fin Adversary Architect: Prism Work-Package 07 — Orchestration, AG-UI & CopilotKit

> **VERDICT — Demo: GO-WITH-FIXES · Prod: NO-GO.**
> The shipped SSE fallback (§D) works end-to-end and the *persisted* dossier is a genuine single
> source of truth, but decorative LLM narration can abort the deterministic result, nothing is
> idempotent or authenticated, the scope gate never enforces scope, and the gated-off AG-UI path
> forks the deterministic composition. Zero tests cover any of it.

---

## Threat Model Summary

- **Topology attacked:** browser → (SSE) `POST /api/v1/reconciliations/stream` → `PrismStreamingOrchestrator`
  → `PrismSweepSteps` + `IReconciliationService` → Azure AI Search + Cosmos + Foundry (gpt-5.4).
  The AG-UI leg (browser → Node sidecar → `MapAGUI("/prism")` → `PrismAgentOrchestrator`) is
  **wired but gated off** (`Prism:AgUiEnabled=false`) and the sidecar + frontend Evidence Stream are
  deferred.
- **Highest-risk assumption:** *"the stream is just a transport over one deterministic source of
  truth."* True for the persisted dossier on the SSE path; **false** for (a) the per-card LLM
  narration that can kill the stream, and (b) the entire AG-UI path, which re-derives the numbers.
- **Lenses:** Reliability (SSE fragility, no timeout/heartbeat), Cost (unbounded LLM fan-out, no
  auth/idempotency), Operational Excellence (200-masks-errors, zero tests), Performance (N+1 corpus
  fetches, triple-recompute on AG-UI).

---

## Findings

### [ARC-01] Decorative narration aborts the deterministic result (P2 inversion) — Severity: **Critical**
- **Target:** `Orchestration/PrismStreamingOrchestrator.cs` L66–L82 (provider loop) and L86–L92
  (fundamentals); `Orchestration/PrismSweepSteps.cs` L76 (`providerExplainer.ExplainAsync`).
- **Attack:** In the SSE sweep, each provider card and the fundamentals card call a Foundry agent
  **without any guard**. `RetrieveProviderRatingAsync` awaits `providerExplainer.ExplainAsync(...)`;
  a single 429 (one gpt-5.4 deployment, no backoff — see pkg-06 STK-04/H4), a per-call hang, or a
  `ConfigurationException` (unset `Azure:OpenAiEndpoint`) propagates out of the `foreach`, out of
  `RunAsync`, into the controller's `catch (Exception)` (`ReconciliationsController.cs` L82–L88) and
  emits `INTERNAL`. **The stream dies before step 4 ever runs — so `reconciliation.RunAsync` is never
  called, the deterministic dossier is never assembled, and nothing is persisted.** The one part of
  Prism that needs no LLM (P2) is killed by the one part that is pure decoration.
- **Impact:** *Availability + the demo's whole thesis.* Under any concurrency (two judges click
  "Reconcile" at once → 429), the STALE-input money-moment never renders. Contrast the REST path,
  which correctly guards narration (`ReconciliationService.cs` L54–L74) and always returns the
  deterministic dossier. The stream inverts that priority.
- **Hardening:** Wrap each per-card narration in the same best-effort try/catch the REST path uses —
  on fault, emit the card with an **empty narrative** (the verbatim rule/verdict is authoritative)
  and continue. Never let a narration fault skip step 4. Better: compute + emit `dossier-ready`
  first (deterministic, cheap), then stream narration as enrichment.
- **Best-practice reference:** WAF Reliability — *degrade, don't fail*; Prism P2 (LLM only narrates,
  never blocks the deterministic core).

### [ARC-02] No idempotency + no concurrency cap → Cosmos flood / hot partition / cost DoS — Severity: **Critical**
- **Target:** `Services/ReconciliationService.cs` L82–L92 (upsert + audit) and L116–L118
  (`NewPartitionedId` = `{issuerId}:{Guid.NewGuid():N}`); `Controllers/ReconciliationsController.cs`
  L17 (`[AllowAnonymous]`); no `AddRateLimiter`/`SemaphoreSlim` anywhere.
- **Attack:** Every `POST /reconciliations` **and** every `POST /stream` with `Confirmed=true`
  synthesises a fresh GUID dossier id + a fresh audit-event id and unconditionally `UpsertAsync`es a
  new document. There is no idempotency key derived from `(issuerId, asOf)`. Because the endpoint is
  anonymous and `Confirmed=true` is a trivially client-set bool, a script (or a reconnecting SSE
  client, or a double-click) can loop `/stream?Confirmed=true` and, per call, (a) persist a duplicate
  dossier, (b) persist a duplicate audit event, and (c) trigger ~10–14 serial gpt-5.4 calls. All
  dossiers for one issuer share partition key `/issuerId`, so a popular issuer becomes a **hot,
  unbounded logical partition** (Cosmos 20 GB logical-partition ceiling; free-tier 1000 RU/s).
- **Impact:** *Cost + availability + storage.* Unbounded LLM spend with no ceiling, Cosmos
  throttling (429) that then trips ARC-01, and monotonic storage growth with no dedupe or TTL.
- **Hardening:** Derive a deterministic id (e.g., `{issuerId}:{asOf:yyyyMMdd}` or a content hash) and
  upsert idempotently, *or* short-circuit to the latest existing dossier for `(issuerId, asOf)`; add
  `AddRateLimiter` (per-caller + global concurrency) before ACA; set a Cosmos TTL on dossiers; make
  the sweep require auth (SEC-01, deferred) before it can persist.
- **Best-practice reference:** WAF Cost + Reliability; REST idempotency (safe ret¬ry); Prism arch-08.

### [ARC-03] AG-UI path re-implements the deterministic composition — two sources of truth — Severity: **High**
- **Target:** `Orchestration/PrismAgentOrchestrator.cs` L232–L246 (`DecomposeAll`), L224–L226
  (`LatestOf`), L177–L197 (`EvaluateRedFlagsAsync` calls `redFlagEngine.Evaluate` directly) vs the
  canonical composer `Services/DossierAssembler.cs` L31–L52.
- **Attack:** `DossierAssembler` is documented as the single pure composer ("it never re-implements
  notch/gap/flag math"). The SSE path honours this — it reads `dossier.Divergences` straight from
  `reconciliation.RunAsync` (`PrismStreamingOrchestrator.cs` L103–L104). But the AG-UI tools
  **bypass `DossierAssembler` entirely**: `decomposeDivergence` and `evaluateRedFlags` re-run the
  same nested pairing loop and `redFlagEngine.Evaluate` locally, and rebuild the `latest` snapshot in
  a third place. Today the two loops are byte-identical, so numbers match — but they are two copies of
  the composition contract. The moment `DossierAssembler` changes (real per-provider vintage
  snapshots for `aInputs/bInputs`, a rating filter, a different ordering), the AG-UI stream silently
  diverges from the persisted REST dossier. Worse: within **one** AG-UI conversation the model calls
  `decomposeDivergence` (compute #1) → `evaluateRedFlags` (re-decomposes internally, compute #2) →
  `assembleDossier` → `reconciliation.RunAsync` → `DossierAssembler` (compute #3) — the divergence is
  computed three times and red flags twice, only the last persisted.
- **Impact:** *Correctness (drift risk) + performance.* Directly threatens acceptance #4 ("same
  dossier object") for the AG-UI transport, and triples deterministic + corpus work per run. Latent
  today because AG-UI is gated off — hence High, not Critical.
- **Hardening:** Route the AG-UI `decompose`/`redflag`/`assemble` tools through `DossierAssembler`
  (or a shared `ComposeDivergences(ordered, latest)` helper) so all three transports call **one**
  function. Have `evaluateRedFlags` consume the divergences produced by `decomposeDivergence` rather
  than recomputing.
- **Best-practice reference:** DRY / single-source-of-truth; Prism P2; WAF Operational Excellence.

### [ARC-04] Scope gate approves a scope it never enforces (P5 partially cosmetic) — Severity: **High**
- **Target:** `Orchestration/PrismStreamingOrchestrator.cs` L31–L36 (`RunAsync` has **no**
  `providers` parameter) and L66 (`foreach (Provider provider in entry.Coverage)`);
  `Controllers/ReconciliationsController.cs` L68 (`orchestrator.RunAsync(request.IssuerId,
  request.AsOf!.Value, request.Confirmed, Emit, ct)` — `request.Providers` dropped);
  `Models/PrismDtos.cs` `ReconciliationStreamRequest.Providers`.
- **Attack:** The stream request carries `Providers`, the `scope-confirm` event echoes a provider
  list, and the gate blocks the sweep until `Confirmed=true`. But the confirmed scope is **never
  applied**: `request.Providers` is silently discarded at the controller boundary and the sweep
  always fans out over the full `entry.Coverage`. A caller can approve "Moody's only," and Prism will
  still reconcile MSCI + DBRS. Additionally the gate is purely **client-enforced** — there is no
  server-side pending-run state binding the approval to a specific `(issuer, asOf, providers)`. A
  client that sends `Confirmed=true` on the first call never sees the gate, and nothing re-verifies
  that the approved scope equals the executed scope.
- **Impact:** *Compliance / trust.* P5 exists to make consequential scope explicit; a gate that
  ignores the scope it displayed is theatre. For a data-quality tool the blast radius is modest
  (extra reads, not trades), but it undermines the "human-in-the-loop" story the rubric rewards.
- **Hardening:** Thread `request.Providers` into `RunAsync` and intersect it with `entry.Coverage`;
  echo the *executed* scope back in `dossier-ready`; if a true pause is desired, mint a server-side
  run token on the unconfirmed call and require it (bound to the scope) on the confirmed call. The
  AG-UI `ApprovalRequiredAIFunction` (real pause) should become the default once un-gated.
- **Best-practice reference:** Prism P5; least-authority; auditable intent.

### [ARC-05] No stream timeout, no heartbeat, no per-LLM-call deadline — Severity: **High**
- **Target:** `Controllers/ReconciliationsController.cs` L48–L88 (no keep-alive frame, no
  `CancellationTokenSource.CreateLinkedTokenSource`/`CancelAfter`); `Orchestration/*` (no per-call
  timeout — grep confirms `Timeout` exists only on the EDGAR/FRED `HttpClient`s at
  `ServiceCollectionExtensions.cs` L45/L55, never on agent calls).
- **Attack:** The SSE handler awaits each gpt-5.4 call with only the ambient `RequestAborted` token.
  A hung upstream (no deadline — mirrors pkg-06 STK-07) stalls the whole sweep indefinitely; only a
  client disconnect frees it. There is no periodic comment/heartbeat frame, so a long gap between
  events (slow model) lets an L7 load balancer or reverse proxy reap the "idle" connection mid-run,
  and the client sees a truncated stream with no terminal event.
- **Impact:** *Reliability + Operational Excellence.* Stuck server threads/connections under load;
  spurious mid-stream disconnects on ACA behind a proxy; no upper bound on request lifetime.
- **Hardening:** Wrap the sweep in a linked CTS with a sane overall deadline and give each agent call
  its own `CancelAfter`; emit a `: keep-alive\n\n` comment frame on an interval (or between steps);
  send a terminal event so the client can distinguish "done" from "dropped."
- **Best-practice reference:** WAF Reliability (timeouts/bulkheads); SSE keep-alive guidance.

### [ARC-06] Zero automated tests for the entire pkg-07 orchestration layer — Severity: **High**
- **Target:** `FinancialServices.Tests/**` — no `PrismStreamingOrchestrator`, `PrismAgentOrchestrator`,
  `PrismSweepSteps`, or `/stream` test exists (file listing confirms only Analysis/Connectors/
  Services tests).
- **Attack:** None of the pkg-07 behaviour is pinned: the HITL pause (unconfirmed → stops), the event
  ordering, cancellation mid-stream, the in-band `error` event, and — most importantly — the
  acceptance-critical **stream/REST parity** ("same dossier object", "identical numbers on repeat
  runs") have no test. The AG-UI fork in ARC-03 would ship green forever. `PrismSweepSteps` is a pure,
  seam-friendly class (fakes already exist: `FakeAgentTextRunner`, `PassthroughDossierNarrator`) so
  the absence is a choice, not a constraint.
- **Impact:** *Operational Excellence.* Regressions in the transport that carries the demo are
  invisible; the acceptance criteria are asserted only by manual live runs.
- **Hardening:** Add xUnit tests over `PrismStreamingOrchestrator` with an in-memory `emit` collector:
  (1) unconfirmed emits only `scope-confirm`+`awaiting-approval` then stops; (2) confirmed emits the
  full ordered sequence; (3) the `dossier-ready` payload equals `service.RunAsync(...).ToResponse()`
  byte-for-byte; (4) a thrown narrator still yields `dossier-ready` (guards ARC-01); (5) a cancelled
  token stops cleanly. Add one asserting the AG-UI `decompose` tool equals `DossierAssembler` output.
- **Best-practice reference:** Prism arch-11; test the seam that ships.

### [ARC-07] N+1 corpus fetches + TOCTOU between streamed cards and the dossier — Severity: **Medium**
- **Target:** `Orchestration/PrismSweepSteps.cs` L66–L68 (`GetProviderRatingsAsync` then
  `FirstOrDefault` — called **once per provider**); `PrismStreamingOrchestrator.cs` L43
  (`ResolveIssuerAsync` → `GetIssuerAsync`) + L99 (`reconciliation.RunAsync` re-fetches issuer +
  ratings).
- **Attack:** For NordStar (3 providers) one stream issues ~2 `GetIssuerAsync` + ~4
  `GetProviderRatingsAsync` Search round-trips, where 1 + 1 suffice: `RetrieveProviderRatingAsync`
  pulls the **entire** ratings list and throws away all but one provider, once per provider, then
  `reconciliation.RunAsync` pulls issuer + ratings **again**. The streamed provider cards and the
  persisted dossier are therefore built from *separate* corpus reads — a time-of-check/time-of-use
  gap (benign while the index is static, a divergence source if it isn't).
- **Impact:** *Performance + Cost (Search RU) + subtle consistency.* Latency and throttle pressure on
  the free-tier Search service; the streamed cards can, in principle, disagree with the dossier.
- **Hardening:** Fetch issuer + ratings **once** at the top of the sweep, pass the in-memory list to
  each step, and hand the *same* list to the composition (an overload of `RunAsync` that accepts
  pre-fetched ratings), so the cards and the dossier are provably the same read.
- **Best-practice reference:** N+1 elimination; WAF Performance Efficiency.

### [ARC-08] Audit write is not atomic with the dossier upsert — Severity: **Medium**
- **Target:** `Services/ReconciliationService.cs` L82 (`store.UpsertAsync(dossier)`) then L85–L92
  (`audit.WriteAsync`), two separate Cosmos writes, no transaction/compensation.
- **Attack:** The dossier is persisted first, then the audit event. A cancellation (client abort mid-
  stream), a Cosmos 429 on the second write, or a crash between the two leaves a **persisted dossier
  with no audit record**. The financial-domain standard requires an audit trail on every data
  mutation; here it is best-effort-after-the-fact, not on the same transactional boundary.
- **Impact:** *Compliance.* Silent audit-trail gaps for real mutations.
- **Hardening:** Use a Cosmos transactional batch (same partition key `/issuerId` for both) so dossier
  + audit commit atomically, or write the audit *before* the dossier and mark it committed, or make
  the audit an outbox the write path drains. At minimum, log + alert on a failed audit write.
- **Best-practice reference:** Financial-domain audit-trail requirement; Prism P6; WAF Reliability.

### [ARC-09] The stream returns HTTP 200 for NOT_FOUND / validation faults — Severity: **Medium**
- **Target:** `Controllers/ReconciliationsController.cs` L50–L54 (status/content-type set to
  `200`/`text/event-stream` up front) + L70–L88 (`NotFoundException`/`ValidationException` surfaced as
  an in-band `error` event, still under a 200).
- **Attack:** An unknown issuer (thrown by `ResolveIssuerAsync` before any byte is flushed) yields
  `200 OK` with an in-band `error` frame rather than a 404. Infra/monitoring (ACA ingress, App
  Insights request success-rate, synthetic probes) see success; only a client that parses the SSE
  body learns it failed. The in-band choice is defensible *after* headers flush, but here it applies
  even to pre-flush errors that could still carry a correct status.
- **Impact:** *Operational Excellence.* Error rates and alerts are blind to a whole class of failures.
- **Hardening:** For faults raised before the first `Emit`, set the real status code (404/400) and
  return; only fall back to the in-band `error` event once the response has started. Emit a
  structured server log/metric per in-band error regardless.
- **Best-practice reference:** WAF Operational Excellence (observable failures); HTTP semantics.

### [ARC-10] AG-UI persistence is model-driven and non-idempotent — Severity: **Medium**
- **Target:** `Orchestration/PrismAgentOrchestrator.cs` L200–L212 (`AssembleDossierAsync` →
  `reconciliation.RunAsync` → persist + audit), driven only by the natural-language instruction
  L100–L112 ("call assembleDossier LAST").
- **Attack:** Whether — and how many times — Prism persists a dossier in the AG-UI path depends on
  the LLM honouring a prose instruction. A model that calls `assembleDossier` twice (or re-runs after
  a tool error) persists two GUID dossiers + two audit events (compounding ARC-02); a model that
  skips it persists none. The deterministic tools (`decompose`/`evaluateRedFlags`) are likewise
  re-invokable at the model's whim.
- **Impact:** *Correctness + Cost.* Non-deterministic side effects (writes) gated on model behaviour.
- **Hardening:** Make persistence a **server-side** step that runs once at end-of-run (outside the
  tool surface the model can call arbitrarily), or guard `assembleDossier` with the idempotency key
  from ARC-02 so repeat calls are no-ops. Keep model-callable tools side-effect-free.
- **Best-practice reference:** Treat LLM output as untrusted control flow; effects behind
  deterministic gates.

### [ARC-11] The "latest" fundamentals snapshot is built in three places — Severity: **Low**
- **Target:** `Services/ReconciliationService.cs` L44–L46, `Orchestration/PrismSweepSteps.cs`
  L88–L92 (`GroundFundamentalsAsync`), `Orchestration/PrismAgentOrchestrator.cs` L224–L226
  (`LatestOf`) — three identical `new FundamentalSnapshot(id, LatestFilingDate, FilingType, null,
  null, null)` constructions.
- **Attack:** When real EDGAR/FRED enrichment lands (the documented seam), whoever updates the
  snapshot must find and change all three or the STALE-input boundary silently differs by transport.
- **Impact:** *Maintainability / latent correctness.*
- **Hardening:** Extract a single `FundamentalSnapshot BuildLatest(IssuerCorpusEntry)` and call it
  from all three.
- **Best-practice reference:** DRY; single source of truth for P3 as-of data.

### [ARC-12] Provider/fundamentals narratives are streamed but never persisted — Severity: **Low**
- **Target:** `PrismStreamingOrchestrator.cs` L80 (emits `Explanation.Text`) vs the persisted
  `ProviderVerdictDto` (`Models/PrismDtos.cs`) which has **no** narrative field; `GET
  /reconciliations/{id}` returns the dossier only.
- **Attack:** The live stream shows narrated provider + fundamentals cards, but a later `GET {id}`
  (or the export) returns a thinner object with those narratives gone — the stream is a superset the
  persisted record can't reproduce. Re-fetch ≠ what the user saw.
- **Impact:** *Reproducibility / auditability asymmetry* (and wasted LLM spend on ephemeral text).
- **Hardening:** Either persist the per-card narratives on the dossier (add fields) or drop them from
  the stream and rely on the persisted red-flag/divergence narration for parity.
- **Best-practice reference:** Prism P3 (auditable, reproducible); Cost.

### [ARC-13] All-singleton composition root is a latent captive-dependency trap — Severity: **Low**
- **Target:** `Infrastructure/ServiceCollectionExtensions.cs` L124–L131 and L168–L170 (every service,
  incl. the orchestrators and `ISearchCorpus`, is `AddSingleton`).
- **Attack:** The all-singleton graph is coherent *today* (stateless engines, thread-safe agent cache
  behind a lock at `PrismAgentOrchestrator` L60–L74). But the auth path (SEC-01) will introduce
  per-request identity (per-user `TokenCredential`, forwarded bearer). Injecting anything scoped into
  these singletons then becomes a captive dependency — a subtle correctness bug.
- **Impact:** *Latent.* Constrains the auth work that must land before prod.
- **Hardening:** When per-user identity arrives, resolve it via `IHttpContextAccessor`/a scoped
  factory rather than constructor-injecting a scoped credential into the singleton orchestrators.
- **Best-practice reference:** .NET DI lifetime rules; documented in the code comment but worth a test.

---

## Design Strengths (kept honest — brief)

- **SSE persisted dossier is genuinely one source of truth.** The shipped path pulls
  `Divergences`/`Flags` from the same `reconciliation.RunAsync` → `DossierAssembler` as REST
  (`PrismStreamingOrchestrator.cs` L99–L109), so the `dossier-ready` payload *is* the REST object.
  Acceptance #4 holds for the default transport.
- **Landmine respected.** `PrismAgentOrchestrator` exposes exactly one AG-UI agent with specialists as
  in-process `AIFunctionFactory` tools (`.cs` L92–L118) — no .NET multi-agent workflow streaming.
- **Fail-loud gating.** AG-UI is off by default and the prerelease hosting package never gates boot;
  when enabled, the agent build throws `ConfigurationException` on an unset endpoint (`.cs` L86–L92).
- **P4 discipline is clean** across the orchestrators, event records, and the model instructions
  (explicit denylist in the prompt).
- **P6/P7 hygiene:** tool args re-authorized against the corpus (`ResolveIssuerAsync`/`ParseProvider`),
  `as-of` clamped to now, `DefaultAzureCredential` only, ids+counts logged (never payloads), and
  `CancellationToken` is plumbed through every async call.
- **Real HITL primitive present** (`ApprovalRequiredAIFunction`) on the AG-UI path — the correct
  mechanism, once un-gated and made the default.

---

## Acceptance Criteria — Met / Partial / Unmet

| # | Criterion | Status | Note |
|---|---|---|---|
| 1 | `POST /prism` (via sidecar) streams gate→provider→fundamentals→waterfall→red-flag e2e | **Unmet as specified / Partial via substitute** | `/prism` + Node sidecar + frontend Evidence Stream deferred; the SSE `/reconciliations/stream` streams the same stage sequence and is the shipped default. |
| 2 | `confirmScope` gate pauses the run until the user approves | **Partial** | SSE stops until `Confirmed=true` (real stop), but no server-side pending state, trivially bypassable, and the approved `Providers` scope is discarded (ARC-04). AG-UI's real `ApprovalRequiredAIFunction` is gated off. |
| 3 | Deterministic tools return identical numbers on repeat runs | **Met (untested at this layer)** | Pure pkg-05 engines are deterministic; no pkg-07 test asserts it (ARC-06). |
| 4 | Fallback REST endpoint produces the same dossier object | **Met for SSE / At-risk for AG-UI** | SSE calls `reconciliation.RunAsync` directly; the AG-UI path composes independently and can drift (ARC-03). No parity test (ARC-06). |

---

## Top 3 Must-Fix

1. **[ARC-01] Guard per-card narration so an LLM 429/hang/misconfig can never skip the deterministic
   dossier.** Emit `dossier-ready` from the deterministic path regardless of narration outcome. This
   is the single biggest on-stage failure risk.
2. **[ARC-02] Make persistence idempotent + rate-limited + auth-gated.** Deterministic dossier id
   from `(issuerId, asOf)`, `AddRateLimiter`, Cosmos TTL — before this runs anonymously on ACA.
3. **[ARC-03] Route the AG-UI `decompose`/`redflag`/`assemble` tools through `DossierAssembler`** (one
   composer for all transports) and add the stream-vs-REST parity test (ARC-06) that would catch any
   future fork.
