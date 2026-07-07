# 🧪 Fin Adversary Stack Critic — Package 10 (Prism UI & Workflow Visualization)

**VERDICT — library-API: GO · type-contract: GO (AMBER on drift-resilience) · build/typecheck: GO**

- **library-API GO** — every recharts / TanStack Query v5 / shadcn-Radix / CopilotKit / lucide API
  used is real and version-coherent with React 18.3.1. No hallucinated component, prop, or hook.
- **type-contract GO** — `frontend/src/types/prism.ts` matches `Models/PrismDtos.cs` field-for-field
  (camelCase, enum member-names verbatim, `DateTimeOffset`→ISO string, `decimal`→number). The wire
  contract was verified against the mapper, the enum, and the actual MVC JSON options. **AMBER**
  only because the contract is hand-maintained with **no runtime validation and stringly-typed
  backend fields** → future drift is a silent runtime failure, not a compile error.
- **build/typecheck GO** — `npm run build` (`tsc --noEmit` strict + vite) green (4325 modules);
  `npm run test` green (36/36, 9 files).

No Critical/High won't-compile / won't-run defect found. The honest adversarial result is that the
stack **fits**; the real exposure is spec-fidelity (the "waterfall" is not a waterfall), a
false-confidence test over dead-for-demo chart code, a write modeled as a query, and the absence of
any drift guard on a hand-typed contract.

---

## Verification Log

| Claim under test | Source / evidence | Verdict |
|---|---|---|
| `Provider` union = backend enum | `Analysis/NotchLadder.cs` L6-10 `Moodys, MorningstarDbrs, Msci` == `types/prism.ts` L13 | ✅ exact |
| Provider serialized as member **name** (not int) | `Program.cs` L28-34 `AddControllers().AddJsonOptions(… JsonStringEnumConverter …)` | ✅ verified in the real MVC path |
| camelCase wire | `AddJsonOptions` default = Web (camelCase); live-verified `provider:"Moodys"` (session log) | ✅ |
| `RedFlagDto.Severity` values | `Analysis/RedFlagEngine.cs` L37/50/67/92 emit `"high"/"medium"` == union `'high'|'medium'|'low'` | ✅ lowercase matches |
| `RedFlagCode` values | RedFlagEngine emits `STALE_INPUT/MISSING_COVERAGE/OUTLIER_PROVIDER/METHODOLOGY_CONFLICT` == union | ✅ exact |
| `Bucket` values | `Analysis/DivergenceDecomposer.cs` L52-54 `"Weighting"/"Input"/"MethodologyAdjustment"` == union | ✅ exact |
| `notches: number` ⟵ `decimal` | `PrismDtos.cs` L36 `decimal Notches`; STJ writes decimal as JSON number | ✅ (see STK note on precision) |
| dates as ISO strings | `PrismDtos.cs` `DateTimeOffset` → JSON string; `prismFormat.formatUtcDate` parses ISO | ✅ |
| TanStack Query **v5** object-form, no v4 `onSuccess`/`onError` | `hooks/useReconciliation.ts`, `hooks/useIssuers.ts`; `@tanstack/react-query@5.59.15` (installed) | ✅ v5-clean |
| recharts primitives real | `Bar/BarChart/Cell/ReferenceLine/ResponsiveContainer/XAxis/YAxis` from `recharts@2.12.7`; `<Cell>`-in-`<Bar>` + `label={{position:'top'}}` are real 2.x API | ✅ real |
| recharts is a **running-total waterfall** | `DecompositionWaterfall.tsx` L108-135 — grouped bars + a "Net gap" bar, no cumulative offset | ❌ **not a waterfall** (STK-01) |
| ResizeObserver stub wired for recharts in tests | `src/test/setup.ts` L4-10 stub; test run did not crash | ✅ wired — but see STK-02 |
| CopilotKit surface | `@copilotkit/react-core@1.4.8` `CopilotKit runtimeUrl=…`; `@copilotkit/react-ui@1.4.8` `CopilotSidebar labels={{title,initial}}`; both gated on `VITE_COPILOT_URL` (App.tsx) | ✅ real, internally consistent |
| No hallucinated generative-UI hooks | grep `useCopilotAction|useCoAgent|useCopilotReadable|useCopilotChat` = **0 hits** | ✅ none referenced without sidecar |
| React-18 peer coherence (installed tree) | recharts `^16‖^17‖^18`; react-query `^18‖^19`; copilotkit `^18‖^19‖rc` — all admit 18.3.1 | ✅ coherent |
| No `any`-cast / `@ts-ignore` hiding drift | grep `as any\|@ts-ignore\|@ts-expect-error` = **0 hits** (only the unavoidable `response.json() as T`) | ✅ clean strict TS |
| Vite proxy `/api → :8000` | `vite.config.ts` L18-21 `target: VITE_BACKEND_URL ?? http://localhost:8000` | ✅ correct |

---

## Findings

### [STK-10-01] The "DecompositionWaterfall" is not a waterfall (spec §B / acceptance drift) — Severity: **Medium**
- **Target:** `frontend/src/components/prism/DecompositionWaterfall.tsx` L108-135
- **Claim under test:** implementationPlan/10 §B — "`DecompositionWaterfall` — recharts `BarChart`
  with a **running total**"; acceptance — "waterfall (leverage 2 + overlay 1)".
- **Reality:** the chart branch renders three **independent** bars (Weighting, Input, Methodology,
  each anchored at 0) plus a fourth `Net gap` bar. There is no transparent base bar / cumulative
  offset, so bars do not stack up to the gap. It is a grouped bar chart named "waterfall". recharts
  2.x *can* do a waterfall (a stacked transparent base + visible delta), but this code does not.
- **Package/version:** `recharts@2.12.7` (verified installed; API used is valid — the defect is the
  transform, not the API).
- **Fix:** either build a true waterfall (`data[i].base = runningTotal`, transparent `<Bar
  dataKey="base" stackId="w" fillOpacity={0} />` + visible `<Bar dataKey="delta" stackId="w" />`),
  or rename to "decomposition bars" and drop the "running total" claim in §B. (Cheapest for the
  demo: rename + delete the redundant `Net gap` bar.)

### [STK-10-02] The recharts chart branch is demo-dead, and its test asserts a wrapper `<div>`, not the chart — Severity: **Medium**
- **Target:** `DecompositionWaterfall.tsx` L106-137; `__tests__/DecompositionWaterfall.test.tsx`
  L24-31; `src/test/setup.ts` L4-10
- **Claim under test:** attack #3 — "verify the waterfall is built correctly" and "does
  ResponsiveContainer need the ResizeObserver stub in tests".
- **Reality (two compounding facts):**
  1. **Dead for the real cast.** The chart branch only renders when `!isResidualDominated`. Per the
     pkg-05 product truth, the live cast is letter-only → `residualShare ≥ 0.8` → **every** real
     divergence (NordStar, Onyx, Cedar Grove) takes the `waterfall-residual` or `waterfall-consensus`
     branch. The recharts `BarChart` is reachable **only** by the synthetic `richDivergence` fixture
     (`src/test/prismFixtures.ts` L169), never by `/api/v1/reconciliations`.
  2. **The test proves nothing about recharts.** L26 asserts `getByTestId('waterfall-chart')`, which
     is the outer `<div>` **wrapping** `<ResponsiveContainer>` (L106). In jsdom, `ResponsiveContainer`
     measures `0×0` (the `ResizeObserver` stub prevents a *crash* but supplies **no dimensions**), so
     recharts renders **no** `<svg>/<Bar>/<Cell>` at all — it logs "width(0) and height(0) … should be
     greater than 0" and returns null children. The legend it also asserts (`waterfall-legend`) is
     plain JSX **outside** the container. Net: **no test ever renders a single recharts primitive**;
     "renders the labelled bar chart" is false confidence.
- **Package/version:** `recharts@2.12.7`, `jsdom@25.0.1`, `vitest@2.1.3`.
- **Fix:** if the chart path is kept, give the container a real size in the test
  (`vi.spyOn(HTMLElement.prototype,'offsetWidth'/'offsetHeight')` or wrap in a fixed `width/height`
  `ResponsiveContainer`) and assert on an emitted `<rect class="recharts-rectangle">`; otherwise
  annotate the branch as demo-dead so the green test is not read as chart coverage.

### [STK-10-03] A non-idempotent POST is modeled as a `useQuery`; safety rests on an incidental `staleTime` interaction — Severity: **Medium**
- **Target:** `frontend/src/hooks/useReconciliation.ts` L16-24
- **Claim under test:** attack #2 — "no mutation-in-useEffect"; queryKey stability; v5 correctness.
- **Reality:** `useReconciliationRun` = `useQuery({ queryFn: () => apiPost('/reconciliations', …) })`.
  The endpoint is a **write** and is **non-idempotent** (pkg-08 ARC-08-01: each call mints a new
  `{issuerId}:{guid}` dossier id + Cosmos upsert + audit doc + ~10 gpt-5.4 calls). Modeling a write
  as a query means any refetch trigger re-executes the write. Mitigations present and correct:
  `staleTime: Infinity` + `refetchOnWindowFocus: false` + `retry: 0` neutralize mount/focus/stale
  refetches. **But** `refetchOnReconnect` (v5 default `true`) is not disabled; it is inert *only*
  because `staleTime: Infinity` makes the query never stale — i.e. duplicate-write safety is an
  emergent side-effect, not a stated intent. The StrictMode rationale in the header comment is
  legitimate (a mutation fired from `useEffect` is torn down by the double-mount), but the v5 idiom
  for that is `useMutation` fired from an **event handler**, or an idempotent run endpoint.
  queryKey `['reconciliation-run', issuerId, asOf]` is stable and correct. ✅
- **Package/version:** `@tanstack/react-query@5.59.15`.
- **Fix:** make the run endpoint idempotent server-side (deterministic `(issuerId, asOf)` id — same
  fix as pkg-08 ARC-08-01) so query re-execution is safe; and/or set `refetchOnReconnect: false`
  explicitly so the no-duplicate-write guarantee is load-bearing rather than incidental.

### [STK-10-04] Hand-maintained contract + stringly-typed backend fields + zero runtime validation → drift is silent, not a compile error — Severity: **Medium**
- **Target:** `frontend/src/types/prism.ts` L52-73; `frontend/src/lib/apiClient.ts` L104;
  `backend/…/Models/PrismDtos.cs` L36, L54-56
- **Claim under test:** attack #1 — "any field the UI reads that the API doesn't emit → silent
  undefined"; the C#↔TS drift scenario.
- **Reality:** the types file is a hand-authored mirror and `apiClient.request` does
  `return (await response.json()) as T` with **no** runtime schema (no zod/valibot). Three wire
  fields are **`string`** on the backend but **narrowed unions** on the frontend:
  `BucketAttributionDto.Bucket` (PrismDtos L36), `RedFlagDto.Code` and `RedFlagDto.Severity`
  (PrismDtos L54-56) — the domain records (`PrismModels.cs`) carry them as raw `string`, not enums.
  Concrete drift that compiles clean on **both** sides and fails silently at runtime:
  - backend emits `"High"` instead of `"high"` → `highSeverityFlags` (prismFormat.ts L74) returns
    `[]` → the money-moment **RedFlagBanner disappears**; `SEVERITY_RANK[sev]` → `undefined` → sorts
    to 99.
  - a renamed/added bucket (`"MethodologyAdj"`, a 4th bucket) → `bucketOf` (prismFormat.ts L34) →
    `undefined` → legend/chart silently show `0`.
  - a 5th red-flag code → `RuleModal`/panel render it, but any `switch`/style keyed on the union has
    no arm.
  There is **no generated client** (NSwag / Kiota / openapi-typescript) even though `/openapi/v1.json`
  is served (`Program.cs` L58).
- **Package/version:** contract spans `@tanstack/react-query@5.59.15` consumers + STJ on .NET; the
  gap is the missing codegen/validation layer.
- **Fix (any one, best first):** (a) generate `types/prism.ts` from `/openapi/v1.json` in CI so drift
  is a red build; (b) validate at the `apiClient` boundary with a zod schema derived from the same
  source; (c) make backend `Bucket`/`Code`/`Severity` real enums + `JsonStringEnumConverter` so the
  wire values are compiler-anchored on the C# side too.

### [STK-10-05] recharts drags a duplicate `react-is@17.0.2` into an 18 app; single 790 kB chunk — Severity: **Low**
- **Target:** installed tree (`node_modules/react-is/package.json` = 17.0.2); build output
  `dist/assets/index-*.js 790.13 kB` (vite warns > 500 kB)
- **Reality:** `recharts@2.12.7` depends on `react-is@^16‖^17‖^18` and resolves `17.0.2` while the app
  runs `react@18.3.1`. Benign (`react-is` is a standalone predicate util, not the renderer) but it
  double-ships and the whole app is one un-split chunk. Not blocking.
- **Fix (optional):** `overrides: { "react-is": "18.3.1" }` in package.json to dedupe; lazy-import
  recharts + CopilotKit (`React.lazy` / `build.rollupOptions.output.manualChunks`) to break the
  chunk.

### [STK-10-06] `RuleModal` mounts `DialogContent` with no `DialogTitle` when `flag` is null (Radix a11y) — Severity: **Low**
- **Target:** `frontend/src/components/prism/RuleModal.tsx` L25-32
- **Reality:** `<DialogContent>` renders `flag ? (<DialogHeader><DialogTitle/>…</>) : null`. Radix
  Dialog requires a `DialogTitle` descendant whenever `DialogContent` is mounted and logs
  "`DialogContent` requires a `DialogTitle`…" otherwise. Latent — every caller sets `open` and `flag`
  together — but a future `open && flag==null` path warns (and is an a11y gap). `@radix-ui/react-dialog@1.1.2`.
- **Fix:** gate on `open && flag` at the `<Dialog>` level, or render a visually-hidden `DialogTitle`
  fallback.

---

## VERIFIED real (this session)

- Provider enum spelling/casing, red-flag codes, severity casing, and bucket strings are emitted by
  the backend **verbatim** and match the frontend unions (grep'd the actual `RedFlagEngine.cs` /
  `DivergenceDecomposer.cs` / `NotchLadder.cs`, not the DTO comments).
- The REST JSON path really does register `JsonStringEnumConverter` (`Program.cs` `AddJsonOptions`)
  — Provider serializes as `"Moodys"`, not `0`. (Note: `PrismJson.Options` is a *separate* copy used
  by Cosmos/handler — pkg-08 STK-02 — but the MVC path independently adds the same converter, so the
  REST wire is correct.)
- TanStack Query **v5** usage is idiomatic: object-form `useQuery`, `enabled` gating, stable
  composite `queryKey`, no removed `onSuccess`/`onError`, no mutation-in-effect.
- recharts/shadcn-Radix/CopilotKit/lucide component + prop names are all real for the pinned
  versions; installed peer ranges all admit React 18.3.1; **no** React-19-only dependency.
- Strict `tsc --noEmit` + `vite build` + `vitest` all green; no `any`-cast or `@ts-*` suppression
  masks the contract.

## COULD-NOT-VERIFY (needs live confirmation)

- **recharts actually renders the chart in a browser** — the chart branch is never exercised by live
  data (STK-02) and never rendered in jsdom (0×0). It compiled and type-checks, but its runtime
  visual has not been proven this session (would need a rich, non-residual divergence in a real
  browser). Everything the demo hits (residual/consensus branches) *is* covered.
- **`decimal` precision on `notches`** — STJ writes `decimal` as a JSON number; for fractional
  Weighting/Input values a trailing-zero form (`"1.0"`) round-trips to JS `1`. Fine for display; not
  re-verified against a live fractional payload (the demo cast is integer notches).
- **CopilotKit runtime** — the provider is gated OFF (`VITE_COPILOT_URL` unset, pkg-07 sidecar
  deferred), so the copilot surface's live behavior is out of pkg-10 scope and unverified here.

---

## What's solid

- **The type contract genuinely matches.** Field-for-field, camelCase, enum-as-name, ISO dates,
  decimals-as-number — verified against the mapper (`PrismDtoMappings.cs`), the domain records, the
  enum, and the real MVC JSON options. I could not find a single field the UI reads that the API does
  not emit, nor a casing/spelling mismatch. This is the hardest thing to get right across three
  ecosystems and it is right.
- **`formatUtcDate`** correctly reconciles `DateTimeOffset` (`…-04:00`) to the UTC calendar date the
  deterministic STALE rule text uses ("2025-09-15") — the board and the rule agree.
- **P2 respected in the UI:** every notch/gap/flag/share is read from the dossier; `prismFormat.ts`
  helpers are pure display derivations (`residualShare`/`isResidualDominated` mirror pkg-05 only to
  *choose a chart treatment*, never to recompute the gap).
- **Honest degradation everywhere:** residual-dominated framing instead of a fake rich waterfall;
  fail-loud `apiClient` (throws `ApiError`, never fabricates); gated CopilotKit (no dead
  `/copilotkit` hammering); empty-narrative → explicit "deferred" note, not invented prose.
- **Workflow tab** uses real SVG (`strokeDasharray`/`markerEnd`) for the dashed escalation edge —
  no library surface to hallucinate; `WorkflowTab` shape is internally consistent.
- **v5 / strict-TS discipline:** no v4 remnants, no `any`, no `@ts-ignore`, StrictMode-safe query
  modeling (the mutation-in-effect trap was correctly avoided).

## Top 3 won't-render-as-intended risks (none are won't-compile)

1. **STK-10-01 / STK-10-02** — the "waterfall" is a grouped bar chart *and* it's demo-dead *and* its
   green test renders zero recharts primitives. If a judge expects the §B running-total waterfall,
   it isn't there, and no test would catch a regression in it.
2. **STK-10-04** — one stringly-typed backend change (`"High"`, a renamed bucket, a new code)
   silently drops the RedFlagBanner or zeroes the legend at demo time; nothing fails the build.
3. **STK-10-03** — the reconciliation "query" re-runs a non-idempotent, ~10-LLM-call write on any
   refetch trigger; it's safe today only via an incidental `staleTime: Infinity` interaction.
