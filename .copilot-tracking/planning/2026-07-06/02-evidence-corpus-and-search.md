# Plan 02 — Synthetic Evidence Corpus & Azure AI Search Index

**Objective:** Author the curated synthetic vault (the "private" lane) with author/role/timestamp
metadata and 3–4 sharp planted needles, then index it in Azure AI Search (Basic) with hybrid + vector
retrieval. **The forensics live in the metadata** — curation beats volume.

**Depends on:** Plan 01. **Primary day:** 1.

> Acceptance test for the whole day: *the mis-ordered approval retrieves with intact metadata.*

---

## 1. Evidence document model

- [ ] Define the vault document schema (JSON) with fields:
  - `id`, `docType` (`ic_minute` | `approval` | `research_note` | `email` | `teams_message` |
    `crm_note` | `valuation_model` | `restricted_list` | `postmortem`)
  - `title`, `body`, `author`, `authorRole`, `createdTs`, `modifiedTs` (ISO-8601, UTC)
  - `decisionId` (groups docs into a decision), `subjectInstrument`, `subjectIssuer`
  - `participants[]`, `citesDocId?` (for the approval→model link), `restricted` (bool)
  - `laneMetadata` — everything the Gap & Red-Flag rules read (see Plan 05)
- [ ] Document the schema in `backend/FinancialServices.Api/data/vault/README.md`

## 2. Author the corpus (40–80 docs, curated — never 15 needles)

Two decisions: the **NordStar Industrials** flagged decision + one **fully-documented control** decision.

- [ ] **The clean backbone:** IC minute naming attendees + rationale (the spine of the chain)
- [ ] **Needle 1 — the gotcha:** a risk-approval doc whose signature `createdTs` is *before* the
      `modifiedTs` of the `valuation_model` file it cites (`citesDocId` links them)
- [ ] **Needle 2 — hindsight fabrication:** an analyst research note dated *after* the trade date
- [ ] **Needle 3 — MNPI proximity:** a Teams thread naming a contact who appears on a planted
      `restricted_list` doc
- [ ] Supporting cast: emails, CRM notes, Outlook items, a postmortem — enough to look real, all with
      consistent author/role/timestamp metadata
- [ ] **The control decision:** a second decision with a fully consistent, all-green document chain
      (approval after its evidence, notes before the trade, no restricted contacts) so Rewind can
      **exonerate**, not just incriminate
- [ ] Store as individual JSON files under `backend/FinancialServices.Api/data/vault/`
- [ ] Rank needles by severity (timestamp gotcha highest) for the demo

## 3. Corpus generation approach

- [ ] Author docs by hand or via a one-off LLM generation script kept in `tools/` (dev-time only,
      never a runtime path) — output is committed static JSON
- [ ] Add a small C# validator (unit test or console) asserting timestamp-consistency of each planted
      needle chain so the "gotcha" can never silently drift when the corpus is edited

## 4. Azure AI Search index (Basic tier — assume provisioned)

⚠ Embedding model at index time MUST match the query-time vectorizer (`text-embedding-3-small`) or
retrieval silently degrades.

- [ ] Define the index schema: searchable `title`/`body`, filterable/sortable `docType`, `author`,
      `authorRole`, `createdTs`, `modifiedTs`, `decisionId`, `restricted`; a `contentVector` field
      (dimensions matching `text-embedding-3-small`) + vector search profile (HNSW)
- [ ] Configure semantic + vector (hybrid) search; keep all timestamp/author/role fields as retrievable
      metadata (the forensics depend on them surviving retrieval)
- [ ] Author an idempotent indexing routine `backend/FinancialServices.Api/data/IndexVaultCommand.cs`
      (or a `tools/` console) that: creates/updates the index, embeds each doc with the **same**
      `text-embedding-3-small` deployment used at query time, and uploads with metadata intact
- [ ] Use `DefaultAzureCredential` for the Search + embedding calls (no keys)
- [ ] Add resilience (retry-with-jitter) around embedding + upload calls

## 5. Retrieval smoke test

- [ ] Author an integration/console test that queries for the flagged decision and asserts the
      **mis-ordered approval doc is returned with `createdTs`/`modifiedTs`/`author` intact** (the day's
      acceptance test)
- [ ] Verify hybrid retrieval returns the IC-minute backbone for the decision query

## Acceptance criteria

- 40–80 committed vault JSON docs, including exactly the ranked needles + a clean control decision
- Timestamp-consistency validator passes (needle chains are internally provable)
- `rewind-vault` index built with `text-embedding-3-small` vectors + full metadata
- Retrieval smoke test: mis-ordered approval retrieves with intact metadata

## Cut-lines

- Drop MNPI/restricted-list needle first (keep timestamp gotcha + hindsight-note) if the demo is tight
  (⚠ mirrors global cut-line #2)
- If AI Search provisioning slips, keep the JSON corpus + a local hybrid stub is **not** allowed
  (no fake clients) — instead demo the corpus via the deterministic timeline (Plan 05) against the
  committed JSON until Search is live
