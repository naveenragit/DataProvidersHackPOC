# Package 05 — Adversarial Review (consolidated)
*2026-07-06 · Prism automated build workflow*

Reviewers: **Prism Standards Adversary** (conformance + P2 + acceptance) + **Fin Adversary Stack
Critic** (decomposition math), plus a **focused Stack re-review** that mutation-tested the fixes.
Security/Architect deferred — pure functions, no I/O/topology (they engage at pkg 07 wiring).

## Verdict
**PASS (GO on the money-moment math).** Build 0 warnings, **123 tests green**, pure/deterministic
(P2), no P4 vocabulary. The invariant reconciles exactly; the STALE flag and the honest
residual-dominance signal are mutation-proven.

## Findings & resolution

| Sev | Finding | Resolution |
|---|---|---|
| Critical | Decomposition was a **residual dump** — Weighting≈0, Input≈0 for letter-only inputs → one-bar waterfall, overclaimed | **Fixed (honest degradation)** — `ResidualShare` + `IsResidualDominated(≥0.8)` wired into METHODOLOGY_CONFLICT; rich `Halcyon` fixture proves Weighting≠0 ∧ Input≠0 with factor+vintage data; `LetterOnly` proves dominance detection; bucket doc no longer overclaims |
| High | STALE_INPUT compared raw `DateTimeOffset` instants → same-UTC-day cross-source data could false-fire | **Fixed** — date-only `.UtcDateTime.Date` both sides; same-UTC-day fixture (mutation-proven); rule reworded to "rating action dated … predates latest filing …" (accurate to real data) |
| High | Property test was tautological (`sum==gap` always true) | **Fixed (mutation-proven)** — recomputes Weighting + Input independently; directed hand-oracles lock the 5m/1m scales + sign; a source sign-flip now fails iteration 0 |
| High | Culture-sensitive date/number rendering in rule text broke P2 reproducibility | **Fixed** — `InvariantCulture` on all rule/summary interpolations |
| High | METHODOLOGY_CONFLICT residual dump without citation | **Fixed** — carries both-provider `EvidenceRefs`; honest `ResidualShare` rule text |
| Medium | ConfidenceScore double-penalized missing coverage (fraction + flag) | **Fixed** — MISSING_COVERAGE excluded from severity penalty (AsterBio ⇒ 2/3) |
| Low | Exact-equality invariant guard could throw on non-terminating decimals | **Fixed** — `0.0001m` tolerance; still throws on real violations |

## The product-truth to carry forward (important for the demo)
The divergence **waterfall is rich only when providers supply factor breakdowns + input vintages**.
For **letter-only real providers**, the gap is residual-dominated — Prism now **detects** this and
should **lead with the STALE flag + "methodology-driven divergence"** rather than a flat waterfall.
This is honest and defensible; curate demo issuers/providers accordingly.

## Residual risks (deferred — tracked)

| Item | Target |
|---|---|
| `Decompose` has no production caller; `IsResidualDominated` is test-only | pkg 07 (feed real per-provider inputs) / pkg 10 (consume dominance signal) |
| Factor-name normalization ("Leverage" vs "Financial Leverage") → no overlap ⇒ Weighting 0 | when real Moody's/DBRS factor taxonomies are known |
| Input attribution needs per-provider vintage snapshots (not supplied by pkg 04's single `latest`) | pkg 06/07 data wiring |
| MISSING_COVERAGE rule string not InvariantCulture-wrapped (no numeric content — benign) | cosmetic |
| EBITDA=EBIT basis marker (from pkg 04 STK-R3) | pkg 06 model revisit |

## Gate
`dotnet build -warnaserror` ✅ 0 warnings · `dotnet test` ✅ 123 passing · pure, no network/LLM.
