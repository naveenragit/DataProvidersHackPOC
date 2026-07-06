# Package 02 — Adversarial Review (consolidated)
*2026-07-06 · Prism automated build workflow*

Reviewers run: **Prism Standards Adversary** (conformance) + **Fin Adversary Stack Critic**
(SDK/domain correctness). **Fin Adversary Architect** and **Fin Adversary Security** were deferred by
orchestrator decision — a pure deterministic math class + records has no topology/coupling, auth,
secrets, PII, or I/O surface to attack; they engage from packages 06 (agents), 08 (API), 11 (deploy).

## Verdict
**PASS** (conditional items resolved). Core attacks failed: the ladder is pure (P2), culture-safe,
order-independent, P4-clean, and domain-accurate (`Gap("A (low)","BBB-") == +3`; AAA=1…C=21;
IG floor BBB-=10; DBRS high/mid/low and Moody's cross-walk correct rung-for-rung).

## Findings & resolution

| # | Sev | Finding | Resolution |
|---|---|---|---|
| 1 | Critical | `ToLabel` silently `Math.Clamp`s out-of-range notches — violates arch/03 "notch 1–21, never swallowed" + P1 fail-loud | **Fixed** — throws `ArgumentOutOfRangeException`; test asserts throw |
| 2 | High | FluentAssertions v8 is commercially licensed (silent-upgrade trap) | **Fixed** — pinned `[6.12.1,7.0.0)` + comment |
| 3 | Medium | `ToNotch(null)` → `NullReferenceException` | **Fixed** — `ArgumentException.ThrowIfNullOrWhiteSpace` |
| 4 | Medium | Unknown label threw `ArgumentOutOfRangeException` (wrong type) | **Fixed** — throws `ArgumentException` |
| 5 | Medium | `TreatWarningsAsErrors` not applied to test project | **Fixed** — `backend/Directory.Build.props` gates both projects |
| 6 | Medium | Moody's case-insensitivity test fed stored casing (tautological) | **Fixed** — feeds differing case (`baa1`→8) |
| 7 | Low | Missing negative tests (out-of-family alias, null) | **Fixed** — added `AAA (high)`, `Aa4` throw tests |
| 8 | Low | DBRS `(mid)` is non-authentic notation | **Accepted** — input alias only, never emitted (comment added) |

## Residual risks (deferred — tracked)

| Item | Target package |
|---|---|
| `NR`/`WR`/`D`/`SD`/`RD` sentinels throw instead of resolving to an "unrated"/default sentinel | 04 (connectors) |
| Positional records use reference equality over `IReadOnlyList` (bites dedup/caching/`.Should().Be()`) | 08 (persistence) — use `.Should().BeEquivalentTo()` meanwhile |
| `decimal` round-trip through Cosmos (Newtonsoft) + Node sidecar (`number`) | 08 (persistence) — fixed-decimal at boundary + STJ serializer |
| Moody's bottom anchor (`C` ≈ S&P `D`) is approximate | document in 04 |

## Gate
`dotnet build` ✅ 0 warnings (warnings-as-errors) · `dotnet test` ✅ 65 passing.
