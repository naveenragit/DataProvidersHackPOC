# Adversarial Stack-Fit Review: RatingOutlook / UnderReview C#↔TS contract

Scope: the just-completed change adding `RatingOutlook` enum, `IG_HY_BOUNDARY` +
`PROVIDER_UNDER_REVIEW` red-flag codes, and optional `Outlook`/`UnderReview` fields on
`ProviderRating` / `ProviderRatingRecord` / `ProviderVerdictDto`, mirrored to `prism.ts`.
Attack goal: prove the wire contract and serialization do NOT actually line up. RESEARCH ONLY.

## Verification Log

- `PrismJson.Options` = `new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } }`
  → [PrismJson.cs L20-24](../../../backend/FinancialServices.Api/Infrastructure/PrismJson.cs) → **verified**.
- MVC pipeline adds `new JsonStringEnumConverter()` + `UnmappedMemberHandling.Disallow`
  → [Program.cs L31-36](../../../backend/FinancialServices.Api/Program.cs) → **verified**.
- `JsonSerializerDefaults.Web` implies camelCase property names, case-insensitive, quoted-number
  reads — and does NOT touch `DefaultIgnoreCondition`
  → learn.microsoft.com/dotnet/api/system.text.json.jsonserializerdefaults → **verified**.
- `JsonSerializerOptions.DefaultIgnoreCondition` default value is **`Never`** (always write)
  → learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions.defaultignorecondition
  → **verified**.
- `JsonStringEnumConverter(namingPolicy = null)` writes enum **member names verbatim** (no policy);
  global `PropertyNamingPolicy` applies only to property names / dictionary keys, not enum values
  → learn.microsoft.com/dotnet/standard/serialization/system-text-json/customize-properties#enums-as-strings
  → **verified** (matches existing `Provider → "Msci"` behavior).
- No connector sets `Outlook`/`UnderReview` to a non-default (grep Connectors/**) → only the record
  definition + passthrough in
  [IProviderRatingsSource.cs L21-22,L40-41](../../../backend/FinancialServices.Api/Connectors/IProviderRatingsSource.cs)
  → **verified**.
- Build is NOT AOT/trimmed (no `PublishAot`/`PublishTrimmed`) → non-generic converter safe today
  → **verified**.

## Findings

### [STK-01] `RatingOutlook` serialization — Severity: PASS (attack failed)
- **Target:** `PrismJson.cs` L23; `Program.cs` L34; `prism.ts` L31.
- **Claim under test:** enums might serialize as integers `0..4` and break the TS string union.
- **Reality:** both serializer configs register a bare `JsonStringEnumConverter()`, which applies to
  ALL enums (factory) and writes member names verbatim. `RatingOutlook.Negative → "Negative"`,
  `.Unknown → "Unknown"`. TS `'Unknown' | 'Positive' | 'Stable' | 'Negative' | 'Developing'` is an
  exact PascalCase match. **No integer mismatch.**
- **Fix:** none.

### [STK-02] Required-in-TS vs optional-in-C# — Severity: PASS (attack failed)
- **Target:** `PrismDtos.cs` L31-32; `prism.ts` L56,L58.
- **Claim under test:** an ignore-condition could omit `underReview:false` / `outlook:"Unknown"` and
  violate the TS `required` fields.
- **Reality:** `JsonSerializerDefaults.Web` leaves `DefaultIgnoreCondition = Never`; no
  `[JsonIgnore]` on the new fields (only `ApiError.Details` has `WhenWritingNull`, L98). STJ therefore
  ALWAYS writes both, including default `false` / `"Unknown"`. Required-in-TS is satisfied on every
  response.
- **Fix:** none. (Do NOT introduce `WhenWritingDefault` globally — it would silently break both
  required TS fields.)

### [STK-03] camelCase property names — Severity: PASS (attack failed)
- **Target:** `PrismDtos.cs` L31-32; `prism.ts` L56,L58.
- **Reality:** Web defaults → camelCase → `Outlook→outlook`, `UnderReview→underReview`. Matches
  `prism.ts`. NOTE: `RedFlag.Code` values (`IG_HY_BOUNDARY`, `PROVIDER_UNDER_REVIEW`) are string
  CONTENT, not property/enum names, so they are emitted UPPER_SNAKE verbatim and match the
  `RedFlagCode` union — the "igHyBoundary-style" phrasing in the ask does not apply.
- **Fix:** none.

### [STK-04] Positional-record back-compat — Severity: PASS (attack failed)
- **Target:** `PrismModels.cs` L37-38; `PrismDtos.cs` L31-32; `IProviderRatingsSource.cs` L21-22.
- **Reality:** the two new params are TRAILING with defaults. All construction/`with` sites compile:
  `ToVerdictDto` passes all 8 positionally incl. the new two
  ([PrismDtoMappings.cs L27-28](../../../backend/FinancialServices.Api/Models/PrismDtoMappings.cs));
  `ToProviderRating` uses named args ([IProviderRatingsSource.cs L33-41]);
  `SyntheticRatingsSource` named-arg ctor stops before the new fields + `record with { AsOfDate }`
  ([SyntheticRatingsSource.cs L21,L55]); tests build `new ProviderRating(...)` with 7 positional args
  ([DivergenceDecomposerTests.cs L57-58]) and a named-arg `ProviderRatingRecord`
  ([ProviderRatingsSourceTests.cs L85-93]). No deconstruction / positional pattern on these records.
- **Fix:** none.

### [STK-05] Streaming path carries the fields — Severity: PASS (attack failed)
- **Target:** `PrismStreamEvents.cs` L37-40 (`ProviderRatingPayload.Verdict : ProviderVerdictDto`).
- **Reality:** the SSE payload nests the same `ProviderVerdictDto`, serialized with `PrismJson.Options`
  ([PrismAgentOrchestrator.cs L62], [ReconciliationsController.cs L58]) → same camelCase + string-enum
  + always-written shape as REST. No TYPED frontend SSE consumer exists (grep found none; AG-UI /
  CopilotKit streaming is explicitly deferred per `DeferredNarrationNote.tsx`, `ScopeNotice.tsx`), so
  nothing on the FE re-declares the verdict shape to drift. REST consumer
  (`useReconciliationRun → DossierResponse`) is fully typed.
- **Fix:** none now; when the SSE consumer lands, reuse `ProviderVerdictDto` (do not re-type inline).

### [STK-06] Exhaustive `Record<RedFlagCode,…>` — Severity: PASS, with Low note
- **Target:** `evidenceCatalog.ts` L93 (`FLAG_PRECEDENTS: Record<RedFlagCode, Precedent[]>`).
- **Reality:** all six keys present incl. `IG_HY_BOUNDARY` (L111) and `PROVIDER_UNDER_REVIEW` (~L176)
  → compiles exhaustively; TS would have errored otherwise. It is the ONLY `Record<RedFlagCode,…>` /
  code-keyed map in the FE (no switch, no other literal map).
- **Low note:** `precedentsForFlag` returns `FLAG_PRECEDENTS[code] ?? []` — the `?? []` silently
  swallows any runtime code outside the union (exactly the drift case), returning empty instead of
  surfacing it. Acceptable as a guard, but it masks contract drift.

### [STK-07] New fields + `PROVIDER_UNDER_REVIEW` are wired but NEVER populated — Severity: Medium
- **Target:** `IProviderRatingsSource.cs` L21-22; `RedFlagEngine.cs` L122; `ProviderVerdictCard.tsx`
  L36-48.
- **Claim under test (won't-RUN, not won't-compile):** does anything actually set `Outlook` /
  `UnderReview`?
- **Reality:** grep of `Connectors/**` shows the ONLY references are the record definition and the
  `ToProviderRating` passthrough — no source (Synthetic, and per grep no Moody's/Morningstar/AI-Search
  connector) ever assigns a non-default. Consequences on the live wire:
  - `outlook` is ALWAYS `"Unknown"`; `underReview` is ALWAYS `false`.
  - `ProviderVerdictCard` badges gated on `verdict.outlook !== 'Unknown'` and `verdict.underReview`
    NEVER render.
  - `RedFlagEngine` `if (r.UnderReview)` (L122) is unreachable → `PROVIDER_UNDER_REVIEW` never fires,
    so its `evidenceCatalog` precedent is dead too.
  - Contrast: `IG_HY_BOUNDARY` (L73) IS reachable — derived deterministically from notches.
- **Fix:** populate `Outlook`/`UnderReview` in the real ratings connector(s) (parse the agency
  outlook / CreditWatch marker — note `NotchLadder` already strips these decorations, so the raw
  marker is available at parse time), or descope the two fields + the flag until a source supplies
  them. As shipped, the contract is honest (P1: no fabrication) but the feature is inert.

### [STK-08] Test overclaims field coverage — Severity: Low
- **Target:** [ProviderRatingsSourceTests.cs L82-93](../../../backend/FinancialServices.Tests/Connectors/ProviderRatingsSourceTests.cs).
- **Reality:** `ToProviderRating_MapsAllFields` is named "carries every field (no silent drop)" but
  constructs `ProviderRatingRecord` WITHOUT `Outlook`/`UnderReview`, so it only ever exercises the
  defaults. A rename/reorder of the two trailing params would pass green.
- **Fix:** set non-default `Outlook`/`UnderReview` in the fixture and assert the round-trip.

### [STK-09] Hand-maintained C#↔TS contract, no generated client — Severity: Low (High-latent)
- **Target:** `prism.ts` header ("keep this file in lock-step … contract-sync rule, arch-09").
- **Reality:** the mirror is correct today, but there is no OpenAPI→NSwag/Kiota generation. A future
  rename (e.g., `UnderReview → OnWatch` in C#) compiles and serializes fine while `prism.ts` stays
  silently stale; only the manual discipline catches it.
- **Fix:** generate `prism.ts` from the OpenAPI document (arch-09 already emits `/openapi/v1.json`),
  or add a contract-drift test.

### [STK-10] Two parallel `JsonSerializerOptions` — Severity: Low
- **Target:** `Program.cs` L31-36 (MVC) vs `PrismJson.cs` L20-24 (SSE + Cosmos).
- **Reality:** configured separately but currently identical. If one later gains a
  `DefaultIgnoreCondition` / naming tweak, REST and SSE outputs diverge for these fields.
- **Fix:** source the MVC converter set from `PrismJson` so there is one definition.

## Unverified (needs live confirmation)
- Whether any real MCP ratings connector (Moody's / Morningstar) that lands later sets
  `Outlook`/`UnderReview` — not present in the current tree; STK-07 assumes the current sources only.
- Whether pkg-11 (ACA) ever enables Native AOT/trimming. If it does, the non-generic
  `JsonStringEnumConverter()` in `PrismJson.cs` L23 and `Program.cs` L34 is `[RequiresDynamicCode]`
  ("Applications should use the generic `JsonStringEnumConverter<TEnum>` instead") and enum-as-string
  would break at runtime. Fine on the current non-AOT build.

## Top 3 Won't-Compile / Won't-Run Risks
1. **Won't-run (no-op feature): STK-07** — `outlook`/`underReview` and the `PROVIDER_UNDER_REVIEW`
   flag are inert because no source populates the values; the UI badges and the flag never appear.
2. **Latent won't-run: AOT** — non-generic `JsonStringEnumConverter` breaks enum serialization if
   AOT/trimming is enabled (Unverified/future).
3. **Silent drift: STK-06 `?? []` + STK-09 hand-mirror** — a code/field the TS union doesn't know
   about is swallowed rather than surfaced.

## Verdict — Contract Fidelity: PASS
The C#↔TS contract is in lock-step and serializes correctly: camelCase property names, PascalCase
enum member-name VALUES (`RatingOutlook`, matching the TS union), always-written defaults (so the
TS `required` `outlook`/`underReview` are never absent), UPPER_SNAKE red-flag code strings, and an
exhaustive `Record<RedFlagCode,…>`. No Critical/High serialization or contract break. The only
substantive issue is FUNCTIONAL, not contractual (STK-07): the new fields and the
`PROVIDER_UNDER_REVIEW` flag are wired end-to-end but never populated, so they are dead on the wire
and in the UI until a real source sets them.
