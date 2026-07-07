# Adversarial Stack-Fit Review: Prism Package 08 — API & Persistence

**Lens:** Fin Adversary Stack Critic (SDK/API accuracy, serialization correctness, version pinning, real-data posture).
**Scope:** `backend/FinancialServices.Api` pkg-08 surface — Cosmos + AI Search persistence/reader, DTO/JSON contract, exception boundary. `net9.0` on the .NET 10 SDK.
**Method:** every SDK call/version/contract treated as guessed-until-verified. Verified against Microsoft Learn API refs + a clean `dotnet restore`/`dotnet list package`. Live Cosmos/Search round-trip behavior is taken from prior session live-verification (NOT re-executed against Azure in this review session — see Unverified).

---

## VERDICT

- **SDK surface: GO.** Cosmos (`Microsoft.Azure.Cosmos` 3.46.0), AI Search (`Azure.Search.Documents` 11.6.0), `IExceptionHandler` (.NET 8+), and `AddOpenApi`/`MapOpenApi` (.NET 9) are all real, correctly-used surfaces; the whole project restores clean (0 NuGet warnings) and builds green under `TreatWarningsAsErrors`.
- **Serialization: GO.** The premised "System.Text.Json-vs-Newtonsoft mismatch" is **REFUTED**: Cosmos is explicitly moved onto STJ via `CosmosClientOptions.UseSystemTextJsonSerializerWithOptions = PrismJson.Options` (first-class integration, not the fragile custom `CosmosSerializer`). camelCase `id`/`issuerId`, enum-as-string, `DateTimeOffset`, and `decimal` all round-trip; `[Required]` on record parameters is the correct target (verified). Newtonsoft.Json 13.0.3 is a correct pin for Cosmos 3.x's internal dependency (not for the dossier documents).
- **Real-data: GO.** No silent in-memory fallback in any runtime path. `GET /issuers` reads the real Search corpus; `POST /reconciliations` and `GET /{id}` hit real Search + Cosmos; the client factories fail loud (`ConfigurationException`) when endpoints are unset — never a fabricated endpoint. Fakes live only in `Tests/TestSupport`.

**No Critical or High stack-fit defects.** Findings are 2 × Medium (both latent/hand-sync coupling, not live breakage) + 3 × Low. This is a well-built package; the aggressive attacks on it mostly failed, and that is stated as verified fact, not praise.

---

## Verification Log (VERIFIED real vs COULD-NOT-VERIFY)

| # | Claim under test | Source | Verdict |
|---|---|---|---|
| V1 | `CosmosClientOptions.UseSystemTextJsonSerializerWithOptions` exists and moves the SDK onto STJ | MS Learn API ref: *"if this option is provided, then the SDK will use the System.Text.Json as the default serializer and set the serializer options as the constructor args"* + clean restore/compile against 3.46.0 | **VERIFIED real** (property documented; compiles in 3.46.0). Docs page currently stamps 3.58.0 — introducing version not independently confirmed, but it demonstrably compiles/links in the pinned 3.46.0. |
| V2 | `[Required]` on a **record constructor parameter** (not `[property:]`) is honored by MVC validation | MS Learn *Model Binding in ASP.NET Core* (aspnetcore-10.0): *"For record types, validation and binding metadata **on parameters is used. Any metadata on properties is ignored**"*; canonical `record Person([Required] string Name, …)` | **VERIFIED real.** The prior `[property: Required]` → `[Required]` fix was correct. `request.AsOf!.Value` is safe — MVC returns 400 before the action runs. |
| V3 | `SearchClient.SearchAsync<T>(string, SearchOptions, CancellationToken)` + `SearchResults<T>.GetResultsAsync()` + `SearchResult<T>.Document` | Azure.Search.Documents 11.6.0 canonical API + green compile under warnaserror + prior live (6 issuers) | **VERIFIED real** (compile + live). |
| V4 | `IExceptionHandler.TryHandleAsync(HttpContext, Exception, CancellationToken) → ValueTask<bool>` + `AddExceptionHandler<T>()` + `AddProblemDetails()` + parameterless `UseExceptionHandler()` | .NET 8+ GA diagnostics API; documented minimal pattern | **VERIFIED real + correct usage.** |
| V5 | Restore is clean; Newtonsoft 13.0.3 satisfies Cosmos 3.46.0; resolved graph | `dotnet restore … -v normal` → **0 warnings**; `dotnet list package --include-transitive` | **VERIFIED.** No NU1605/NU1608. Resolved: Newtonsoft.Json **13.0.3**, Azure.Core **1.44.1**, System.Text.Json **10.0.9**, OpenAI **2.10.0**, System.ClientModel **1.14.0**. |
| V6 | camelCase `Id`→`id` / `IssuerId`→`issuerId` (Cosmos `id` + `/issuerId` partition-path requirement) | `PrismJson.Options = new(JsonSerializerDefaults.Web){…}` (Web ⇒ camelCase + case-insensitive) + `JsonStringEnumConverter` | **VERIFIED by construction** (Web defaults) + prior live write success (partition-key match is load-bearing and worked). |
| U1 | Cosmos **3.46.0's** internal `id`/partition-key extraction uses STJ identically to the item payload | Reasoned from V1 doc + prior live round-trip; 3.46.0 source not read | **COULD-NOT-VERIFY by source** — verified only by prior live behavior. |
| U2 | Cosmos 3.46.0 / Search 11.6.0 executing against the **uplifted** STJ 10.0.9 at runtime | Compiles + prior live; not re-run on a clean machine this session | **COULD-NOT-VERIFY (this session).** |
| U3 | Live `POST → Cosmos persist → GET point-read` round-trip | Prior session live-verification (recorded), **not re-executed here** | **COULD-NOT-VERIFY (this session)** — relying on recorded prior run. |

---

## Findings

### [STK-01] Dossier `id` partition-prefix and the persisted partition value come from two different sources — Severity: **Medium** (latent)
- **Target:** `Services/ReconciliationService.cs` L46 (`string dossierId = NewPartitionedId(issuerId);`) + L118 (`$"{issuerId}:{Guid.NewGuid():N}"`, built from the **request** param) vs `Services/DossierAssembler.cs` L52 (`IssuerId: issuer.IssuerId`, the **corpus** value) vs `Services/CosmosDossierStore.cs` L34 (`new PartitionKey(dossier.IssuerId)`) and `Controllers/ReconciliationsController.cs` L160-168 (`PartitionOf` re-derives the partition from the id prefix).
- **Claim under test:** "Point-read `ReadItemAsync<T>(id, new PartitionKey(issuerId))` matches how the doc was written — no off-by-one on the id vs partition value."
- **Reality:** Write partition value = `dossier.IssuerId` = `issuer.IssuerId` (**corpus-stored** casing). The dossier **id** prefix = the **request** `issuerId`. `GET /{id}` recovers the partition by splitting the id prefix (request casing). These are equal today **only transitively**, because the AI Search OData filter `issuerId eq '{escaped}'` (`Services/SearchCorpus.cs` L46, L67, L73) is a **case-sensitive exact match** on `Edm.String` — so `GetIssuerAsync` can only succeed when the request `issuerId` byte-equals the stored value. Add a `normalizer` to the `issuerId` field (the standard "make issuer search case-insensitive" change) and the guarantee breaks: `POST "NordStar"` matches stored `"nordstar"`, persists a dossier in partition **`nordstar`** with id **`NordStar:{guid}`**; the follow-up `GET NordStar:{guid}` point-reads partition **`NordStar`** → **404 on a document that exists**.
- **Fix:** Derive the id from the canonical corpus value so the prefix and the partition value share one source: `string dossierId = NewPartitionedId(entry.Issuer.IssuerId);` (already in scope in `RunAsync`). Optionally normalize `issuerId` once at the controller boundary.

### [STK-02] `PrismJson` is advertised as the single serializer source "shared by MVC" — but MVC configures its own options, and `PrismJson.Create()` is dead — Severity: **Medium** (contract-drift risk)
- **Target:** `Infrastructure/PrismJson.cs` L8-11 doc (*"Shared by MVC, the exception handler, and the Cosmos serializer so the shape never drifts between the three."*) + L23 `Create()`; `Program.cs` L28-34 (MVC `AddJsonOptions` builds its **own** `JsonSerializerOptions`, adding `JsonStringEnumConverter` + `UnmappedMemberHandling.Disallow` from scratch — it does **not** reference `PrismJson`).
- **Claim under test:** single canonical wire format across MVC responses, the SSE stream, the error envelope, and Cosmos.
- **Reality:** Two independently-configured option instances. `PrismJson.Options` is used by the SSE stream (`ReconciliationsController.cs` L58), Cosmos (`ServiceCollectionExtensions.cs` L106), the exception handler (`PrismExceptionHandler.cs` L42), and the AG-UI orchestrator (`PrismAgentOrchestrator.cs` L62). MVC responses use the **separate** Program.cs instance. `grep` confirms `PrismJson.Create()` has **zero call sites** — it is dead code, and its comment ("MVC needs its own mutable copy") describes wiring that does not exist. The two configs are only *coincidentally* identical (both Web defaults + `JsonStringEnumConverter`); a converter added to one and not the other silently diverges the REST payload from the SSE/error/Cosmos payload. This is real cross-surface contract drift inside a single process — exactly the hand-synced-contract failure mode, minus a generated source of truth.
- **Fix:** Configure MVC from the shared source — e.g. `AddJsonOptions(o => PrismJson.Apply(o.JsonSerializerOptions))` (extract a single `Apply`/builder), and delete or actually use `Create()`. Then correct/keep the "never drifts" claim truthfully.

### [STK-03] Version-interop: the serialization layer runs on a transitively **uplifted** STJ 10.0.9 (a full major past the target and past what Cosmos/Search shipped against) — Severity: **Low** (green-but-fragile)
- **Target:** `FinancialServices.Api.csproj` L19-38; resolved graph from `dotnet list package --include-transitive`.
- **Claim under test:** the pinned pkg-08 SDKs are a coherent matrix.
- **Reality:** pkg-08 pins are fine in isolation, but the **effective** runtime is pulled up by the pkg-06/07 Agent Framework graph: `System.Text.Json` resolves to **10.0.9** (via `Microsoft.Extensions.AI.OpenAI 10.6.0` / `Microsoft.Agents.AI[.OpenAI] 1.13.0`), and `OpenAI` to **2.10.0** under `Azure.AI.OpenAI 2.1.0`. So the Cosmos STJ serializer (`UseSystemTextJsonSerializerWithOptions`) and the MVC JSON pipeline both execute on an STJ major that neither Cosmos 3.46.0 nor Search 11.6.0 shipped/tested against. It restores clean and worked live (STJ is strongly back-compatible), but this is the same "GA pin is half-true; the base floats up 9 minors" fragility already logged in pkg-06/07 STK-01 — and here it sits directly under pkg-08's core (persistence serialization).
- **Fix:** Add explicit pins (`System.Text.Json`, `OpenAI`, `System.ClientModel`) so the resolved base is intentional, and add a CI transitive-version gate (`dotnet list package --include-transitive`) to catch silent uplifts on the next restore.

### [STK-04] `PartitionOf` splits the id on the **first** `':'` — Severity: **Low**
- **Target:** `Controllers/ReconciliationsController.cs` L160-168.
- **Reality:** `id.IndexOf(':')` recovers the partition as everything before the first colon. Safe today (issuerIds are `nordstar`, `cedargrove`, … — no colons; the guid is `:N` = 32 hex, no colons). But it silently couples correctness to "issuerId never contains `':'`" with no guard; an issuerId with an interior colon would point-read the wrong partition → spurious 404, or a `ValidationException`.
- **Fix:** Split on the **last** `':'` (the guid separator), or use a delimiter that cannot appear in an issuerId, or store the partition key as an explicit second path segment rather than encoding it in the id.

### [STK-05] `decimal` round-trip — premise checked, largely REFUTED — Severity: **Low** (note)
- **Target:** `Models/PrismModels.cs` (`BucketAttribution.Notches`, `RatingFactor.Weight/Score` — `decimal`); persisted via the Cosmos STJ serializer.
- **Reality:** With STJ (not Newtonsoft, not `double`), `decimal` is written as a full-precision JSON **number** and parsed back to `decimal` without going through binary floating point — so the API/DTO layer and the Cosmos layer serialize the same value identically. The only residual concern is Cosmos DB's own numeric storage precision for *very high-precision* decimals; Prism's notch contributions are small, low-precision values (tenths/units from `DivergenceDecomposer`), so that ceiling is never approached. **No precision loss for the value ranges in play.** Documented here because it was an explicit attack vector; no code change needed.

---

## Attacks that FAILED (verified-solid, not praise)

- **"Cosmos serializes with Newtonsoft → STJ attribute mismatch corrupts docs."** Refuted. `ServiceCollectionExtensions.cs` L106 sets `UseSystemTextJsonSerializerWithOptions = PrismJson.Options`; per MS Learn this switches the SDK's default serializer to STJ, so the dossier documents are STJ end-to-end. There is no STJ-vs-Newtonsoft round-trip on the payload. `[JsonPropertyName]` is not even needed on the domain records because the shared Web-defaults options give camelCase consistently on write and case-insensitive matching on read.
- **`id` / partition-key naming.** camelCase (`JsonSerializerDefaults.Web`) makes `Id`→`id` (Cosmos's required lowercase key) and `IssuerId`→`issuerId` (matching the `/issuerId` partition path). Enum-as-string via `JsonStringEnumConverter` (no naming policy ⇒ member name verbatim, e.g. `"Moodys"`, `"MorningstarDbrs"`, `"Msci"`) matches `frontend/src/types/prism.ts`.
- **Cosmos system fields on read.** `PrismJson.Options` deliberately does **not** set `UnmappedMemberHandling.Disallow` (that is only on the MVC request-binding options in `Program.cs` L33). So `ReadItemAsync<ReconciliationDossier>` tolerates `_rid/_ts/_etag/_self/_attachments` — correct separation; had `Disallow` been shared onto Cosmos, every read would throw.
- **Positional records through STJ + `[Required]` target.** Single-ctor positional records deserialize via the parameterized constructor with camelCase/case-insensitive matching (`IReadOnlyList<T>` materializes to `List<T>`); no `[JsonConstructor]` needed. `[Required]` on record **parameters** is the framework-correct target (V2).
- **Search API + field names.** `SearchCorpusRow` pins every field with `[JsonPropertyName]` (serializer-policy-independent), matching the pkg-03 seeded `prism-ratings` index; the SDK surface is real and live-verified.
- **Point-read partition discipline.** `CosmosDossierStore` always passes an explicit `PartitionKey` for both `ReadItemAsync` and `UpsertItemAsync` (no cross-partition scan); a `404` read maps to `null` → HTTP 404, other `CosmosException`s fail loud as `UpstreamServiceException` (502). `IExceptionHandler` correctly returns `false` for `OperationCanceledException` (P7).
- **Real-data posture (P1).** No silent fallback anywhere in runtime code; the real `CosmosClient`/`SearchClient` are registered with `DefaultAzureCredential` and throw `ConfigurationException` (naming the setting) when unconfigured. Acceptance #1 (`GET /issuers` from real Search) is met by `IssuersController → ISearchCorpus → SearchClient`.
- **Restore/version hygiene.** Clean restore, no NU1605/NU1608; Newtonsoft 13.0.3 satisfies (and safely exceeds) Cosmos 3.46.0's Newtonsoft floor.

---

## Top 3 won't-run / won't-round-trip risks (ranked)

1. **STK-01** — id/partition dual-source coupling: a future `issuerId` normalizer (case-insensitive search) makes `GET /{id}` return 404 on persisted dossiers. Latent, but a plausible one-line "improvement" trips it. One-line fix.
2. **STK-02** — MVC serializer config is hand-duplicated (not `PrismJson`), and `PrismJson.Create()` is dead: REST vs SSE/error/Cosmos payloads can silently diverge on the next converter change.
3. **STK-03** — the persistence serialization layer runs on a transitively-uplifted STJ 10.0.9 / OpenAI 2.10.0 base; green today, but the resolved matrix is unintentional and ungated.

## Unverified — do not assume true (needs live confirmation)
- **U1:** Cosmos **3.46.0**'s internal id/partition extraction uses STJ identically to the payload (reasoned + prior live only; source not read).
- **U2/U3:** Runtime behavior of Cosmos 3.46.0 / Search 11.6.0 on STJ 10.0.9, and a live `POST → persist → GET` round-trip, were **not re-executed in this review session** — they rest on prior recorded live verification. Re-run one `POST /api/v1/reconciliations` (NordStar) + `GET /api/v1/reconciliations/{id}` against live Azure before the demo to reconfirm.
