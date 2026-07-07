# Adversarial Architecture Review: Live Morningstar + Moody's MCP integration (PLAN)

**Target:** `.copilot-tracking/planning/2026-07-07/live-provider-mcp-integration.md`
**Reviewer:** Fin Adversary Architect · **Date:** 2026-07-07 · **Nothing implemented — plan under review.**

---

## VERDICT

**APPROVE-WITH-CHANGES — must-revise before any code.** The connector seam, the *ingest-not-hot-path*
choice (D3-a), the Key-Vault token model (D4), and the honest-degradation primitives are sound, and the
grounded "consume, don't build, MCP" correction is genuinely valuable. But the plan **misdiagnoses its own
central risk (R1)** and its recommended data-flow (D3-a) carries a **Critical silent-corruption defect whose
stated mitigation is inert**. Both invalidate the Phase-0 gate as written. Do not start implementation until
C1 and C2 are fixed in the plan.

**Severity counts:** 2 Critical · 6 High · 4 Medium · 3 Low.

---

## Threat Model Summary

- **Topology attacked:** ops-laptop one-time OAuth → provider MCP → (sidecar or C#) → **refresh job** →
  upsert `ratingCard` docs into Azure AI Search `prism-ratings` → **existing** deterministic reconciliation
  read path (`SearchCorpus.GetProviderRatingsAsync` → `SearchCorpusMapper.MapCards` → `DossierAssembler` →
  `DivergenceDecomposer` / `RedFlagEngine` / `ReconciliationScoring`).
- **Highest-risk assumption:** "the deterministic core *requires* per-provider factor weights/scores, so
  Phase-0 must confirm them" (§0, §2, R1). **This is false against the current code** and it aims the go/no-go
  gate at the wrong criteria.

---

## Findings

### [ARC-01] D3-a creates a dual-source-of-truth that SILENTLY corrupts every deterministic output — Severity: **Critical**
- **Target:** Plan §3 D3-a ("preserves synthetic docs behind a `source` field", "idempotent by (issuerId,
  provider, asOf)"); §4 diagram; §6 R5. Code: [SearchCorpus.cs](backend/FinancialServices.Api/Services/SearchCorpus.cs#L63-L72), [SearchCorpusMapper.cs](backend/FinancialServices.Api/Services/SearchCorpusMapper.cs#L84-L92), [DossierAssembler.cs](backend/FinancialServices.Api/Services/DossierAssembler.cs#L36-L44), [ReconciliationScoring.cs](backend/FinancialServices.Api/Analysis/ReconciliationScoring.cs#L42), [RedFlagEngine.cs](backend/FinancialServices.Api/Analysis/RedFlagEngine.cs#L58-L72).
- **Attack:** The read path is `docType eq 'ratingCard' and issuerId eq '{id}'` with **no `source` /
  precedence filter**, and `MapCards` returns **one `ProviderRating` per card** with `AsOfDate <= asOf`
  (`SearchCorpusMapper.MapCards`). Ingest a **live** Moody's card while the **synthetic** Moody's card still
  exists for the same issuer (both action-dated ≤ asOf) and reconciliation now sees **two `Provider.Moodys`
  ratings**. Consequences, all silent:
  - `ReconciliationScoring.ConfidenceScore` computes `coverage = ratings.Count / 3`. Four ratings ⇒
    `coverage = 1.33`, only hidden by `Math.Clamp(...,0,1)` → confidence math is structurally broken.
  - `DossierAssembler` enumerates all `i<j` pairs → it pairs **Moody's-synthetic vs Moody's-live**; if the
    letters differ, `RedFlagEngine` emits `METHODOLOGY_CONFLICT` reading *"Moodys vs Moodys differ by N
    notches"* on stage, plus doubled Moody's-vs-Morningstar pairs.
  - `ConsensusSummary` spread and `OUTLIER_PROVIDER` median are computed over a set containing a duplicate →
    the headline "N-notch split" and the outlier flag can be manufactured or suppressed by a self-pair.
- **Impact:** Correctness + compliance. The **money moment** (notch gap, consensus, flags) is silently wrong.
  This violates **P1** (silent, not fail-loud) and **P2** (deterministic core returns wrong numbers). The
  plan's mitigation ("preserve synthetic behind a `source` field") **touches only the write** and does nothing
  to the read query — it is non-mitigating. The proposed write key `(issuerId, provider, asOf)` actively
  *guarantees* coexistence because live and synthetic have different action dates → different docs → both read.
- **Hardening:** Decide precedence explicitly and enforce it at **read** time: either (a) upsert live under a
  **deterministic doc id per `(issuerId, provider)`** so live overwrites synthetic, or (b) add a `source`
  field and make `GetProviderRatingsAsync` select **exactly one card per provider** (live > synthetic, then
  latest `AsOfDate ≤ asOf`). Add a `DossierAssembler` guard that fails loud if `ratings` contains a duplicate
  `Provider` (P1). Pin it with a test: synthetic+live NordStar ⇒ exactly 3 ratings, no self-pair.
- **Best-practice reference:** WAF Reliability (correctness under data evolution); P1/P2.

### [ARC-02] R1 is misdiagnosed — the deterministic core does NOT require factor weights and ALREADY runs letter-only / 100% residual-dominated — Severity: **Critical**
- **Target:** Plan §0 ("biggest risk to the whole idea"), §2 ("`Factors[]` … needed by `DivergenceDecomposer`"),
  §5 Phase-0 ("ideally factor weights"), §6 **R1**. Code: [DivergenceDecomposer.cs](backend/FinancialServices.Api/Analysis/DivergenceDecomposer.cs#L34-L52), [DossierAssembler.cs](backend/FinancialServices.Api/Services/DossierAssembler.cs#L39), [SearchCorpusMapper.cs](backend/FinancialServices.Api/Services/SearchCorpusMapper.cs#L69), [ReconciliationService.cs](backend/FinancialServices.Api/Services/ReconciliationService.cs#L72-L74).
- **Attack:** Trace what `Decompose(a, b, latest, aInputs, bInputs)` actually consumes:
  - `gap = b.Notch - a.Notch` — needs **Letter only** (via `NotchLadder`).
  - **Weighting bucket** — iterates factors present in **both** providers; today the Search read path sets
    `Factors: Array.Empty<RatingFactor>()` (`SearchCorpusMapper.ToProviderRating`) ⇒ **always 0**.
  - **Input bucket** — `LeverageContribution(bInputs) - LeverageContribution(aInputs)`, driven by
    `FundamentalSnapshot.DebtToEbitda` **from EDGAR**, not from the ratings provider. `DossierAssembler`
    passes `aInputs: null, bInputs: null` and `ReconciliationService` builds `latest` with
    `DebtToEbitda: null` ⇒ **always 0**.
  - **MethodologyAdjustment** = residual = `gap - 0 - 0 = gap` ⇒ `ResidualShare = 100%`,
    `IsResidualDominated = true`.

  So the core **already** runs exactly the "degraded" state R1 fears — confirmed independently by the pkg-10
  review (ARC-10-02/STK-10-02: "DecompositionWaterfall … DEAD on real cast … all residual-dominated"). Live
  letter-only data changes the decomposition's behavior by **nothing**. Meanwhile the *real* minimum-viable
  payload — **letter (parseable) + rating-action date + a mappable id** — is under-weighted, and factor
  weights (a genuine non-requirement, already discarded even for the synthetic source) are elevated to the
  headline gate.
- **Impact:** The Phase-0 **go/no-go gate tests the wrong thing.** You could green-light a provider because it
  returns factors yet still fail on id-mapping, or red-light one for "no factors" that the core never needed.
  The plan also implicitly over-promises a 3-bucket waterfall "money moment" that does not exist end-to-end
  today (the Weighting bucket is dropped by the read mapper even for the factor-bearing synthetic source).
- **Hardening:** Rewrite Phase-0 exit criteria around **letter (normalizable to a `NotchLadder` notch) +
  rating-action date + issuer-mappable id**; mark factor weights **"bonus, upgrades Weighting from residual
  to explicit — not gating."** Separately decide whether to **keep one factor-bearing synthetic provider**
  alongside live letter-only providers to preserve a real waterfall, and fix the read mapper that currently
  discards synthetic factors — otherwise the demo's decomposition value from "going live" is zero.
- **Best-practice reference:** WAF Operational Excellence (validate the true constraint before building); P2.

### [ARC-03] Licensing gate (D5) is sequenced AFTER the architecture it can veto — Severity: **High**
- **Target:** Plan §3 D5, §5 **Phase 5** ("Licensing/attribution review sign-off before any live data is
  persisted or exported"), §6 R3.
- **Attack:** D3-a's entire value is **persisting** provider ratings into Search (caching, as-of history,
  offline demo). If the Morningstar/Moody's entitlement forbids caching/redistribution/export — the plan
  itself flags this as *likely* — then D3-a is **not implementable as designed**, and the dossier PDF export
  (pkg 08/10) is a contractual breach. Discovering this in Phase 5 means Phases 1–4 (broker, MCP client,
  mappers, ingestion, Key Vault, ACA wiring) were built against an illegal architecture.
- **Impact:** Cost + compliance; a late licensing "no" invalidates most of the build.
- **Hardening:** Move the licensing determination **into Phase 0** as a hard gate with two documented
  branches: **(persist-allowed)** → D3-a ingest as planned; **(no-persist)** → session-only / in-memory live
  reads on the hot path (D3-b) with a live-latency banner, or drop live entirely. Do not build the ingestion
  path until the persist/redistribute right is confirmed in writing.
- **Best-practice reference:** WAF Operational Excellence (external blocking dependencies gate the design).

### [ARC-04] id-mapping (issuer↔provider entity id; issue-level vs issuer-level) is co-critical, not R8-tier — Severity: **High**
- **Target:** Plan §6 **R8** (listed near the bottom), §5 Phase-0 ("keyed by an id Prism can map"). Code:
  [SearchCorpusMapper.cs](backend/FinancialServices.Api/Services/SearchCorpusMapper.cs#L95-L100) (`Issuer` carries a single `SampleBondIsin` + `Cik`).
- **Attack:** Prism reconciles **per issuer**; `Issuer` holds one `SampleBondIsin` and one `Cik`. Provider
  ratings are frequently **issue-level (per-bond, per-CUSIP/ISIN)** — a single issuer has many CUSIPs. If the
  provider exposes only issue-level ratings, "the issuer's notch" is undefined without a selection/aggregation
  rule, and you cannot fetch **any** rating without an issuer→provider-entity-id crosswalk. This gates the
  fetch as hard as R1 gates the shape, yet it sits below the fold.
- **Impact:** Correctness; a wrong or missing crosswalk yields wrong ratings or blanket `MISSING_COVERAGE`.
- **Hardening:** Promote id-mapping into the Phase-0 go/no-go. Capture, per provider: entity id type
  (LEI/CUSIP/ISIN/provider-internal), whether ratings are issuer- or issue-level, and (if issue-level) the
  deterministic rule that maps the sample bond → issuer notch. Unmapped issuer → `MISSING_COVERAGE`, never a
  guess (P1).
- **Best-practice reference:** P3 provenance; WAF Reliability.

### [ARC-05] `NotchLadder` / `ParseProvider` fail LOUD on real-world grades — one `WR`/`NR` kills issuer ingestion — Severity: **High**
- **Target:** Plan §5 Phase-2 mapper ("any field the provider doesn't return is left null"). Code:
  [NotchLadder.cs](backend/FinancialServices.Api/Analysis/NotchLadder.cs#L37-L41), [SearchCorpusMapper.cs](backend/FinancialServices.Api/Services/SearchCorpusMapper.cs#L111-L116).
- **Attack:** **Good news the plan omits:** `NotchLadder` already maps Moody's (`Aaa..C`) and DBRS
  (`(high)/(mid)/(low)`) — letter→notch is *not* a Phase-0 unknown for these two. **Bad news:** `ToNotch`
  **throws `ArgumentException`** on any grade it doesn't know, and `ParseProvider` throws on any provider label
  that isn't the exact enum name. Live payloads carry `NR`, `WR` (Moody's withdrawn), `SD`/`D`, provisional
  `(P)Baa2`, watch `Baa2 *-` / `RUR`, outlook `BBB (negative)`, structured `(sf)`. A single such value throws
  during ingestion/mapping → per P1 that's "correct," but operationally it **kills that issuer's card
  ingestion** on one benign qualifier.
- **Impact:** Reliability; brittle ingestion, noisy failures on normal ratings-agency vocabulary.
- **Hardening:** Add a **normalization/allowlist** step before `ToNotch`: strip outlook/watch/provisional
  suffixes to the base grade; map `NR/WR/SD/D` (and unknowns) to **`MISSING_COVERAGE`** (null record) rather
  than throwing. Keep fail-loud for a genuinely corrupt grade, but do not let `WR` == outage.
- **Best-practice reference:** WAF Reliability (graceful handling of expected input variety); P1.

### [ARC-06] STALE_INPUT money-moment needs LIVE filing dates too — plan wires live ratings only — Severity: **High**
- **Target:** Plan §2 (live data "only changes the inputs"), §5 Phase-2/3. Code:
  [RedFlagEngine.cs](backend/FinancialServices.Api/Analysis/RedFlagEngine.cs#L29-L41), [ReconciliationService.cs](backend/FinancialServices.Api/Services/ReconciliationService.cs#L68-L74).
- **Attack:** `STALE_INPUT` compares each provider's `InputAsOfDate` (rating-action day) against
  `latest.FilingDate` (`filingDay`). `ReconciliationService` builds `latest` from the **corpus issuer doc**
  (`entry.LatestFilingDate`) and *deliberately does not* invoke EDGAR on synthetic CIKs. Wire in a **live**
  rating action date but leave the filing boundary sourced from a synthetic/corpus issuer doc, and the
  money-moment flag pits a **real rating action against a fabricated filing date**, citing
  `edgar:{Cik}:{filingType}` for a synthetic `Cik`. Wrong flag and/or fabricated-looking citation.
- **Impact:** Correctness + provenance (P3). The single most important flag can fire (or not) on mismatched
  vintages.
- **Hardening:** Treat "go live" as **both** planes: for any issuer served with live ratings, the recon path
  must also pull the **real EDGAR filing date** (pkg 04 is built but unwired here) so the STALE comparison is
  real-vs-real and as-of aligned. Add this as an explicit Phase-3 work item, or restrict live ratings to
  issuers with real EDGAR coverage.
- **Best-practice reference:** P3 (as-of correctness, no hindsight, real citations).

### [ARC-07] Multi-replica refresh-token rotation race on ACA — normal scale-out can revoke the token — Severity: **High**
- **Target:** Plan §3 D4, §4 diagram, §5 Phase-1/Phase-4, §6 R2 ("refresh + rotation").
- **Attack:** OAuth 2.1 with **rotating** refresh tokens (the plan explicitly notes rotation + 60s skew) plus
  a single Key-Vault-stored token plus **≥1 ACA replica each reading+refreshing** = a classic race: replica A
  refreshes, the AS **rotates** and invalidates the old refresh token; replica B, mid-refresh with the stale
  token, gets `invalid_grant` → auth failure. Worst case the stored token is burned and R2's "re-login
  runbook" is triggered by a routine scale event. The plan has no single-flight/leader/lock and no Key-Vault
  optimistic-concurrency (ETag) write-back.
- **Impact:** Reliability + operational; provider-wide auth outage under horizontal scale — the exact
  condition production runs in.
- **Hardening:** Serialize refresh: a single-flight refresher (distributed lock / leader, or a dedicated
  refresh job that writes the token; replicas only *read* access tokens), Key-Vault write with ETag
  precondition + retry-on-conflict, and a cached in-memory access token with jittered pre-expiry refresh.
  Document max replica assumptions until this exists.
- **Best-practice reference:** WAF Reliability (idempotent, concurrency-safe shared state).

### [ARC-08] "Core untouched / reconciliation path unchanged" is contradicted by the fix ARC-01 requires — Severity: **High**
- **Target:** Plan §0, §2 ("Deterministic core is out of scope and must not change"), §3 D3-a
  ("Reconciliation path is unchanged"), §7.
- **Attack:** The plan sells D3-a on "reconciliation path unchanged." But the only correct fix for ARC-01 is
  **source precedence / one-card-per-provider in `SearchCorpus.GetProviderRatingsAsync` and/or `MapCards`** —
  which **is** the read path feeding `DivergenceDecomposer`/`RedFlagEngine`. So the plan must either (a) edit
  the read path (contradicting "unchanged") or (b) ship the ARC-01 corruption. It cannot have both, and it
  does not acknowledge the tension.
- **Impact:** Governance honesty; a stated invariant ("we don't touch the core read path") is unachievable
  alongside correctness.
- **Hardening:** Own it: state that the **read-selection** layer (not the notch/gap/flag math) changes to add
  precedence, keeping the *deterministic Analysis/ engines* untouched (P2 preserved). Redraw the boundary as
  "Analysis/ math unchanged; corpus read gains a precedence filter."
- **Best-practice reference:** P2 (scope the invariant precisely); WAF Operational Excellence.

### [ARC-09] Connector seam is orphaned from the hot path — Phase-3 needs an un-scoped inverse writer — Severity: **Medium**
- **Target:** Plan §0/§2 ("feed through the existing honest connector seam"), §5 Phase-3. Code:
  [ReconciliationService.cs](backend/FinancialServices.Api/Services/ReconciliationService.cs#L45), [SearchCorpusMapper.cs](backend/FinancialServices.Api/Services/SearchCorpusMapper.cs#L60-L69) (read-only, Search→domain).
- **Attack:** Reconciliation reads **Search**, not `IProviderRatingsSource`. The connectors are only reachable
  via the Phase-3 refresh job, which must convert `ProviderRatingRecord` → the `prism-ratings` `ratingCard`
  document (`SearchCorpusRow`) — the **inverse** of the existing read-only `SearchCorpusMapper`, plus new
  index fields (`source`, a real `ratingActionDate`/`asOfDate`) and a reseed. None of this is called out; the
  plan writes "upsert `ratingCard` docs" as if the writer exists.
- **Impact:** Under-scoped; hidden work + a new mapping surface to test.
- **Hardening:** Add explicit Phase-3 items: inverse `ProviderRatingRecord → SearchCorpusRow` projection,
  index schema delta (`source` + action-date semantics), the SeedData/ingest writer, and a round-trip test
  (write live card → read back → correct `ProviderRating`).
- **Best-practice reference:** WAF Operational Excellence.

### [ARC-10] Morningstar-first sequencing bets on the least-confirmed capability — Severity: **Medium**
- **Target:** Plan §3 D6, §5 Phase-0 ("Morningstar MCP may be fund/equity research … if it does not expose
  DBRS-style credit ratings, this provider is out"), §8 Q6.
- **Attack:** The plan's own text concedes `mcp.morningstar.com` may be Morningstar research (funds/equity),
  and **DBRS is a distinct product**. "Land Morningstar first to prove the pattern" therefore front-loads the
  provider **most likely to fail** the Phase-0 data-existence test; if it's out, the "prove the pattern"
  sequence collapses with Moody's still unconfirmed.
- **Impact:** Schedule risk; the primary path may evaporate at the first gate.
- **Hardening:** Make Phase-0 provider-agnostic and **prove the pattern on whichever provider Phase-0 confirms
  first**. Don't pre-commit "Morningstar-first" in the plan; commit after discovery.
- **Best-practice reference:** WAF Operational Excellence (sequence around validated capability).

### [ARC-11] "4–6 days" is optimistic against the un-scoped surface — Severity: **Medium**
- **Target:** Plan §5 ("~4–6 focused days after a green Phase 0").
- **Attack:** Not counted or under-counted: ARC-09 inverse writer + index delta + reseed; ARC-05 grade
  normalization; ARC-04 id crosswalk + issue↔issuer rule; ARC-01/ARC-08 read-path precedence + guard;
  ARC-07 refresh single-flight + ETag; Key Vault + ACA managed identity + RBAC; ARC-06 live EDGAR wiring;
  and ARC-03 licensing, an **external blocking dependency with unbounded latency**. Phase-0 itself depends on
  the user obtaining Moody's URL/auth.
- **Impact:** Schedule credibility.
- **Hardening:** Re-estimate after Phase-0 with the above as line items; treat licensing and Moody's-pack
  arrival as calendar gates, not effort days.
- **Best-practice reference:** WAF Operational Excellence.

### [ARC-12] D2 sidecar-vs-native left "pick either" understates the sidecar's ops cost — Severity: **Medium**
- **Target:** Plan §3 D2, §4 diagram, §6 R6, §8 Q1.
- **Attack:** The Python MCP-broker sidecar adds: a second deploy unit + run script; a **new cross-service
  span boundary** (correlation-id must propagate C#→sidecar or traces break); a new **single point of failure**
  (sidecar down ⇒ *all* providers dark during the refresh job); and internal-REST latency — all to reuse
  wealthgen's `mcp_oauth`/`mcp_client`. The C# side already has OTel, DI, Polly, and Key Vault; a native MCP
  JSON-RPC-over-Streamable-HTTP client is a bounded, testable component. "Pick either" hides that the sidecar
  costs more operationally for a job that runs on a schedule (latency is not the constraint).
- **Impact:** Operational excellence + reliability; polyglot footprint for marginal reuse.
- **Hardening:** Default to **native C#** unless Phase-0 shows the MCP client is materially cheaper in Python.
  If the sidecar is chosen, require: health/liveness, restart policy, W3C trace-context propagation, and a
  "sidecar-down ⇒ refresh job fails loud, live cards untouched, synthetic remains" degradation contract.
- **Best-practice reference:** WAF Operational Excellence / Reliability.

### [ARC-13] Phase-0 has no written measurable pass bar — Severity: **Low**
- **Target:** Plan §5 Phase-0 ("Confirm there is a tool returning corporate-bond issuer credit ratings").
- **Attack:** "Confirm" with no threshold isn't a gate. Per ARC-02/ARC-04, the pass bar must be explicit.
- **Hardening:** Write Phase-0 PASS = *"for ≥1 in-scope issuer, the provider returns a base letter grade
  normalizable to a `NotchLadder` notch, a rating-action date, and an id mappable to a Prism issuerId;
  factors optional."* Anything less = provider out, stated plainly.
- **Best-practice reference:** WAF Operational Excellence.

### [ARC-14] Dynamic client registration (RFC 7591) vs pre-registered secret is undecided and has rotation impact — Severity: **Low**
- **Target:** Plan §1 (Morningstar `/register` supported), §8 Q4.
- **Attack:** A dynamically registered client mints its **own** `client_secret` that must **also** land in Key
  Vault and be re-registerable on rotation/expiry — a different secret-lifecycle than a pre-issued client_id.
  Left open, it leaks into Phase-1 design.
- **Hardening:** Pick pre-registered `client_id/secret` if the provider issues them; only use `/register` if
  required, and if so specify where the registered secret is stored and how it re-registers.
- **Best-practice reference:** P6.

### [ARC-15] Hardcoded `MCP-Protocol-Version: 2025-06-18` copied from the sample is brittle — Severity: **Low**
- **Target:** Plan §1 (transport table), Phase-2.
- **Attack:** Pinning a single MCP protocol date means a provider protocol rev can break every call with no
  negotiation path.
- **Hardening:** Treat the protocol version as config; log the server's negotiated version from `initialize`;
  alert on mismatch.
- **Best-practice reference:** WAF Reliability.

---

## What's sound (kept honest — brief)

- **The `IProviderRatingsSource` seam genuinely exists** and `null → MISSING_COVERAGE` is the right honest
  degradation primitive (P1). Extending it rather than inventing a parallel path is correct.
- **D3-a (ingest over hot-path calls) is the right call** for demo determinism, reproducibility, and
  rate-limit friendliness. The critique in ARC-01/ARC-08 is about **read-time precedence**, not the choice to
  ingest.
- **D4 Key Vault over the sample's `data/oauth/*.json`** is correct (P6), and the plan correctly identifies the
  hard constraint that ACA has no browser → one-time dev-box interactive login is unavoidable.
- **Refusing to fabricate factors** (leave null, stay residual-honest) aligns with P1/P2. R1's *handling* is
  right even though its *severity framing* is wrong (ARC-02).
- **The grounded MCP/OAuth verification is real and valuable:** the "consume, don't build, MCP servers"
  correction and the fetched Morningstar RFC-8414 AS metadata (authorize/token/register, PKCE S256, no
  client_credentials) are accurate and de-risk the auth design.
- **`NotchLadder` already covers Moody's + DBRS scales** — a real strength the plan fails to credit; letter→
  notch is *not* an open risk for exactly these two providers (only unknown *qualifiers* are — ARC-05).

---

## Top must-fix before implementation begins (prioritized)

1. **[ARC-01, Critical]** Add **read-time source precedence / one-card-per-provider** (live > synthetic, then
   latest ≤ asOf) plus a `DossierAssembler` duplicate-`Provider` fail-loud guard, with a synthetic+live
   NordStar test. The "source field on write" mitigation as written is inert.
2. **[ARC-02, Critical]** Rewrite Phase-0 exit criteria around **letter + rating-action date + mappable id**
   (factors = bonus, not gating), because the core already runs letter-only/residual-dominated today; and
   decide how to preserve a real waterfall (keep a factor-bearing synthetic provider + fix the factor-dropping
   read mapper) or drop the 3-bucket "money moment" claim.
3. **[ARC-03, High]** Move the **licensing persist/redistribute determination into Phase 0** as a hard gate
   with a documented no-persist branch; do not build D3-a ingestion until the right is confirmed in writing.
4. **[ARC-04, High]** Promote **id-mapping (issuer↔entity id, issue-vs-issuer ratings)** into the Phase-0
   go/no-go — it gates the fetch as hard as data-shape does.
5. **[ARC-06 + ARC-05, High]** Wire **live EDGAR filing dates** for any live issuer so `STALE_INPUT` compares
   real-vs-real, and add **grade normalization/allowlist** (`NR/WR/SD` → `MISSING_COVERAGE`, strip outlook/
   watch) so one qualifier isn't an outage.
6. **[ARC-07, High]** Specify a **single-flight, ETag-guarded refresh** before any >1-replica ACA deploy.
7. **[ARC-08, High]** Correct the plan's "reconciliation path unchanged" claim to "Analysis/ math unchanged;
   corpus **read** gains a precedence filter."

**Do not approve for implementation until #1 and #2 are corrected in the plan** — they change the Phase-0 gate
and the data-flow, i.e. the two decisions everything else is built on.
