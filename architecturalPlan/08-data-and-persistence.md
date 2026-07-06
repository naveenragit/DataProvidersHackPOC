# 08 — Data & Persistence

Two stores: **Azure AI Search** (the synthetic ratings corpus) and **Cosmos DB** (dossiers + audit).
Plus **real** external data via connectors (as-of correct).

---

## Cosmos DB

- Database `prism`. `CosmosClient` registered as a **singleton** with `DefaultAzureCredential` and
  camelCase serialization (accelerator `AddAzureClients`).

| Container | Partition key | Contents | Query patterns |
|---|---|---|---|
| `rating_reconciliations` | `/issuerId` | `ReconciliationDossier` | by id+issuer (point read), by issuer (list) |
| `audit_events` | `/issuerId` | audit records | by issuer, by date |

- **Prefer point reads** (`ReadItemAsync(id, new PartitionKey(issuerId))`) — always pass the partition
  key. Only query when the partition key is unknown; never do cross-partition scans in a hot path.
- Documents are immutable once written (dossiers, audit). Regenerate = new id, don't mutate.
- Cosmos **free tier** (1000 RU/s, 25 GB) — opt in at account creation. Do not enable Cosmos vector
  search for this corpus (small, curated) — AI Search is the vault store (HACKATHON-FINDINGS §7).

## Azure AI Search (the corpus)

- Index `prism-ratings`. Fields per [implementationPlan/03](../implementationPlan/03-synthetic-data-and-search-index.md):
  `id`, `docType`, `issuerId`, `provider`, `asOfDate`, `inputAsOfDate`, `content`, `contentVector`.
- Hybrid (keyword + vector) + semantic ranking. Retrieval always **filters** by `issuerId` (+`provider`
  for provider agents).
- **Embedding model must match at index time and query time** (`text-embedding-3-small`, 1536 dims) or
  retrieval silently degrades. Pin the model name in one place.
- `as-of` metadata (`asOfDate`, `inputAsOfDate`) is mandatory on every rating card — it drives the
  stale-input flag and the citations.
- Seeding via `tools/SeedData` is **idempotent** (upsert by `id`); safe to re-run during rehearsal.

## Real external data (as-of correctness)

- EDGAR/FRED/Treasury via `Connectors/` only. Every read applies the **as-of filter**: use the latest
  value with timestamp ≤ the decision date. **No hindsight** — never read observations after `asOf`.
- Capture provenance: EDGAR `LatestFilingDate`/`FilingType`, FRED series id + observation date. These
  become citations.
- On missing real fields, mark `unavailable` — never fabricate (P1).

## Data ownership

- `SearchCorpus` service owns Search reads; `ReconciliationService` owns Cosmos reads/writes;
  `AuditService` owns audit writes. Controllers/agents go through these services, not the SDK clients
  directly.
