# Plan 10 — Dossier Export (PDF)

**Objective:** One-click export of the paginated, regulator-ready dossier with **every claim
hyperlinked to its source document**. A **pre-rendered PDF fallback is mandatory** (not optional) — the
live export is the first cut-line.

**Depends on:** Plans 06, 09. **Primary day:** 4.

> ⚠ Risk register: in-browser paginated, hyperlinked PDF is a classic 1–2 day time-sink. Pre-rendered
> fallback is **mandatory**; live export stays one click and is cut first.

---

## 1. Pre-rendered fallback (build this FIRST — mandatory)

- [ ] Produce a polished, paginated PDF of the NordStar dossier (and the control dossier) ahead of
      time, stored as a static asset the UI can open on click
- [ ] Every claim in the pre-rendered PDF hyperlinks to its source doc (internal anchors or a citations
      appendix listing doc id + timestamp)
- [ ] Wire the export button to open the pre-rendered PDF instantly — this guarantees the demo's
      2:35–3:00 export beat regardless of live-render status

## 2. Dossier content

- [ ] Cover: inquiry (issuer, instrument, decision date), submitted-by, generated timestamp, elapsed
      reconstruction time (~90s) vs manual baseline (~3 weeks)
- [ ] The two-lane timeline (rendered), ordered event chain, the ranked red flags with the deterministic
      rule text + cited docs/timestamps for each
- [ ] The as-of market snapshot (observation-date framing note)
- [ ] Full citations appendix — every claim → source doc id + timestamp (audit-grade provenance)

## 3. Live export (nice-to-have, first cut-line)

- [ ] `GET /api/v1/reconstructions/{id}/dossier/export` returns a generated PDF (server-side render via
      a headless renderer or a C# PDF lib) built from the persisted dossier
- [ ] Writes an `AuditEvent` (`dossier_exported`) before returning (Plan 06)
- [ ] Frontend export button prefers live; falls back to the pre-rendered asset on any error/timeout
- [ ] Feature flag in Settings (Plan 08) toggles live vs pre-rendered

## Acceptance criteria

- Export button always yields a paginated dossier PDF in ≤1 click (pre-rendered path never fails)
- Every claim is traceable to a source doc id + timestamp
- Live export, when enabled, matches the pre-rendered content and writes an audit event
- Control-decision dossier exports all-green

## Cut-lines

- Live PDF export → pre-rendered dossier PDF opened on click (⚠ global cut-line #1). The timeline
  (Plan 09) stays live either way.
