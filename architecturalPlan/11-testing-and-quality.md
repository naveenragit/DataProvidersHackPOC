# 11 — Testing & Quality

Test what judges will probe — the deterministic core, the money moment, and the API contract. Mocks
live **only** here.

---

## Tooling

- Backend: **xUnit** + **FluentAssertions**; mock external boundaries with **NSubstitute**/**Moq**.
  Integration via `WebApplicationFactory<Program>` (`Microsoft.AspNetCore.Mvc.Testing`).
- Frontend: **vitest** + **@testing-library/react**.
- Coverage target: **≥80% on business logic and agents** (deterministic engines → aim ~100%).

## Must-have backend tests (priority order)

1. **NotchLadder** — every provider label maps (incl. DBRS `(high)/(low)` + Moody's aliases); IG/HY
   boundary at `BBB-`; `Gap` sign correctness.
2. **DivergenceDecomposer invariant** — `Weighting + Input + MethodologyAdjustment == NotchGap` for all
   pairs (property-style test over the issuer cast).
3. **RedFlagEngine** — NordStar → exactly one `STALE_INPUT` (cites MSCI card + EDGAR date); Cedar Grove
   → zero flags; Aster Bio → `MISSING_COVERAGE`; Onyx → `OUTLIER_PROVIDER`.
4. **EDGAR as-of filtering** — stubbed `HttpMessageHandler`: `asOf` before Q3 returns Q2 only; after Q3
   returns Q3 with correct `LatestFilingDate`.
5. **Narrator-honesty** — given a narration that contradicts a deterministic number, the validator
   discards it and keeps the deterministic value.
6. **API integration** — `GET /api/v1/issuers` returns the cast; `POST /api/v1/reconciliations` for
   NordStar persists a dossier with the flag; error responses match the standard shape.

## Must-have frontend tests

- `DivergenceBoard` renders a 3-notch split and highlights the widest-gap pair.
- `RuleModal` shows the verbatim `RedFlag.Rule` + both dated source rows.
- `DecompositionWaterfall` bars sum to the notch gap.

## Rules

- **No mocks/fakes in runtime code** — only in test projects (P1). Tests may stub EDGAR/FRED/Foundry.
- Deterministic engine tests run with **no network, no LLM**.
- Keep tests fast and deterministic; no reliance on live Foundry in unit tests (a small live-smoke
  suite is separate and run during rehearsal).

## Quality gates

- `TreatWarningsAsErrors` on; nullable on; build is clean.
- Run `get_errors` / `dotnet build` + `dotnet test` and `npm run test` before marking a package done.
- **Live smoke (rehearsal):** full NordStar + Cedar Grove paths against live Foundry, twice, before the
  demo (see [implementationPlan/12](../implementationPlan/12-demo-testing-and-cutlines.md)).
