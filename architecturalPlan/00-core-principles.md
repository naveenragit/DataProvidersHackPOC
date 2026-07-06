# 00 — Core Principles (non-negotiable)

These override convenience. If a change would break one of these, stop and reconsider the approach.

---

## P1 — Real Azure only, fail loud

- No mock data, stub clients, or canned responses in runtime code. Mocks live **only** in test
  projects (`xUnit`, `vitest`).
- No silent fallbacks. If a service is unconfigured/unreachable, throw a clear error that names the
  missing setting or failing dependency. Missing required config fails at **startup**
  (`ValidateOnStart`) — never substitute placeholders.

## P2 — The deterministic core (the rule that keeps the demo honest)

- **Notch math, gap ordering, attribution, and every red-flag trigger are plain C#** — in
  `Analysis/` (`NotchLadder`, `DivergenceDecomposer`, `RedFlagEngine`). They take inputs and return
  outputs with **no network and no LLM**.
- **The LLM only narrates and cites.** It never invents or overrides a number. Narrator output is
  validated against the deterministic values; on conflict, drop the narrative and show the raw rule.
- When a flag fires, the UI shows the **exact deterministic rule text**. This is what defeats
  "you rigged it" skepticism.

## P3 — Provenance & citation

- Every provider claim traces to a source doc id (`sourceRef`); every red flag carries `EvidenceRefs`.
- Real fundamentals are **as-of correct**: only data with timestamp ≤ the decision date is used. No
  hindsight. Say "observation-date filter," not "point-in-time vintage."

## P4 — Domain language discipline

- **Say:** reconciliation, divergence, provenance, as-of, coverage, notch gap, data quality,
  methodology attribution, auditable, cited.
- **Never say:** buy / sell / hold / recommend / allocate / trade / position sizing / alpha / signal.
- Prism explains **why data disagrees** — it is a data-quality/reconciliation tool, **not** a trading
  agent. This appears in code comments, prompts, UI copy, and the pitch.

## P5 — Human-in-the-loop before consequential actions

- The reconciliation sweep pauses at a **confirm-scope gate** (`ApprovalRequiredAIFunction` →
  CopilotKit `renderAndWaitForResponse`). Gates render **amber/orange** in the UI.

## P6 — Security & privacy by default

- `DefaultAzureCredential` everywhere; never hardcode keys. Secrets via env/user-secrets/Key Vault.
- Treat all LLM-provided tool arguments as hostile; the API re-authorizes them.
- Never log PII or raw financials — log ids + counts. Content Safety on any user free-text.

## P7 — Async & cancellation end-to-end

- All I/O is `async`/`await` with a `CancellationToken` plumbed through every call.

## P8 — Reuse the accelerator

- Build on `templates/` (csharp-api, frontend-design-system, workflow-visualization) and the
  `.github` kit. Call this reuse out in the demo (rubric: Creativity & reuse).

---

**If you are an AI agent:** confirm your change honors P1–P8 before finishing. If a request conflicts
with a principle, surface the conflict rather than silently violating it.
