# Plan 09 — The Two-Lane Black-Box Timeline UI

**Objective:** The big UI investment (Day 3, no library shortcut). Build the two-lane black-box
timeline: private vault lane vs public market lane, streaming evidence cards via CopilotKit, the
red-arrow flag animation, the scope-confirmation gate, and the stopwatch vs 3-week ghost baseline.
**Legibility in 90 seconds is the bar.**

**Depends on:** Plans 05, 06, 07, 08. **Primary day:** 3.

> ⚠ Top risk: timeline illegible in 90s → the gotcha lands flat in a wall of JSON. Two lanes **max**;
> the red-arrow animation is rehearsed. This day is dedicated to this component.

---

## 1. Timeline layout

- [ ] `frontend/src/components/timeline/BlackBoxTimeline.tsx` — two horizontal lanes:
  - **Private lane** (what the firm knew): vault evidence, ordered by timestamp
  - **Public lane** (what the market knew): as-of yields/spreads + publication-stamped news
- [ ] Shared time axis; the **information gap** between lanes is visually obvious (the compliance
      finding). Keep it to two lanes — resist a third
- [ ] Each event is a node with timestamp, source label, author/role (private) or publisher (public)

## 2. Streaming evidence cards (CopilotKit generative UI)

- [ ] As the orchestrator calls each specialist (Plan 04 function tools), render a live **evidence
      card** via CopilotKit generative UI (`useCoAgent` / `renderAndWait` patterns over AG-UI SSE)
- [ ] Cards stream in during the forensic sweep (demo 0:35–1:15): vault hits, then the rebuilt tape
- [ ] Cards settle into their lane positions as the timeline assembles (1:15–1:45)

## 3. The red-arrow flag animation (the money moment)

- [ ] When a `RedFlag` fires, animate a **red arrow** across the timeline connecting the two cited
      events (e.g., approval ← → cited valuation model), pointing out the out-of-order timestamps
- [ ] Clicking the flag opens a detail panel showing:
  - the **deterministic rule** text (`approvalTs < modelEditedTs`) — proves it's not LLM guesswork
  - **both source documents**, each deep-linked, with their timestamps highlighted
- [ ] Severity-rank multiple flags; the timestamp gotcha is primary
- [ ] Rehearse the animation timing (1:45–2:15 in the script)

## 4. Scope-confirmation human gate

- [ ] Render the confirm-scope gate (Plan 04 `ApprovalRequiredAIFunction` →
      `renderAndWaitForResponse`) as an amber/orange gate card the user must approve before the sweep
      (governed-agents-on-display moment, 0:20–0:35)
- [ ] On approve, call `POST /confirm-scope` (server-side gate, Plan 06) and resume streaming

## 5. Stopwatch + baseline

- [ ] A stopwatch starts on inquiry submit; a ghost "manual baseline ~3 weeks" bar sits alongside
- [ ] On dossier assembly, the stopwatch stops (~90s) next to the 3-week ghost (0:00 and 2:35–3:00)

## 6. Control run (exoneration)

- [ ] Support running the **all-green control decision**: same UI, timeline assembles, **no red arrow**,
      panel reads "fully defensible." Proves Rewind exonerates, not just incriminates (2:15–2:35)

## 7. Accessibility & polish

- [ ] Legible at a glance: large type for timestamps, clear lane labels, high-contrast red flag
- [ ] No buy/sell/recommend language anywhere in labels or narration (compliance framing only)
- [ ] Reduced-motion fallback for the animation

## Acceptance criteria

- Two-lane timeline renders the NordStar reconstruction legibly within ~90 seconds
- Evidence cards stream live during the sweep; scope gate blocks then resumes
- The red arrow fires on the timestamp gotcha; clicking shows the rule + both cited docs
- The control decision renders all-green with no flag
- Stopwatch vs 3-week ghost is visible start and end

## Cut-lines

- Live PDF export is **not** here (Plan 10) — the timeline stays live regardless
- If streaming cards are unstable, render cards from the final dossier object (poll `GET /dossier`)
  instead of live SSE — the two-lane timeline + red arrow is the non-negotiable core
- MNPI/hindsight flags can be narrated; keep the timestamp red arrow as the one rehearsed animation
