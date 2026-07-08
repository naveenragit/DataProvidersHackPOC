// frontend/src/types/prism.ts
//
// TypeScript mirror of the Prism REST DTO contract (see architecturalPlan/09).
// Wire format is camelCase (System.Text.Json default) and C# enums serialize as their
// member NAMES (e.g. "MorningstarDbrs").
//
// LIVE-VERIFIED CONTRACT (package 10, 2026-07-06): these shapes were re-synced against the
// running API on http://localhost:8000 — GET /issuers, POST/GET /reconciliations, and the
// 404 envelope. `Models/PrismDtos.cs` (package 08) is the authoritative source; keep this
// file in lock-step on any future contract change (contract-sync rule, arch-09).

/** Rating provider — mirrors the C# `Provider` enum member names verbatim. */
export type Provider = 'Moodys' | 'MorningstarDbrs' | 'Msci'

/** Attribution bucket for a decomposed notch gap. */
export type Bucket = 'Weighting' | 'Input' | 'MethodologyAdjustment'

/** Deterministic red-flag codes emitted by the RedFlagEngine. */
export type RedFlagCode =
  | 'STALE_INPUT'
  | 'IG_HY_BOUNDARY'
  | 'MISSING_COVERAGE'
  | 'OUTLIER_PROVIDER'
  | 'PROVIDER_UNDER_REVIEW'
  | 'METHODOLOGY_CONFLICT'

/** Red-flag severity. */
export type Severity = 'high' | 'medium' | 'low'

/** Forward-looking rating outlook (direction, not level). Mirrors the C# `RatingOutlook` enum. */
export type RatingOutlook = 'Unknown' | 'Positive' | 'Stable' | 'Negative' | 'Developing'

/** A corporate-bond issuer in the reconciliation cast (list projection). */
export interface IssuerListItem {
  issuerId: string
  legalName: string
  ticker: string
  cik: string
  sector: string
  sampleBondIsin: string
  /** Providers with coverage for this issuer. Always present (verified live vs GET /issuers). */
  coverage: Provider[]
}

/** One provider's verdict for an issuer, with the as-of dates behind it. */
export interface ProviderVerdictDto {
  provider: Provider
  letter: string
  notch: number
  /** ISO 8601 date-time (C# DateTimeOffset). */
  asOfDate: string
  /** ISO 8601 date-time — date of the financials the rating is built on (drives STALE_INPUT). */
  inputAsOfDate: string
  methodologyDocId: string
  /** Forward-looking outlook; 'Unknown' when the source did not supply one. */
  outlook: RatingOutlook
  /** True when the provider has the issuer on CreditWatch / under review. */
  underReview: boolean
}

/** One attribution bucket's signed contribution to a provider pair's notch gap. */
export interface BucketAttributionDto {
  bucket: Bucket
  /** Signed notch contribution to the pair gap. */
  notches: number
  explanation: string
  evidenceRefs: string[]
}

/** The notch gap between two providers, decomposed into attribution buckets. */
export interface PairDivergenceDto {
  a: Provider
  b: Provider
  notchGap: number
  attribution: BucketAttributionDto[]
}

/** A deterministic red flag with its verbatim rule text and supporting evidence. */
export interface RedFlagDto {
  code: RedFlagCode
  severity: Severity
  rule: string
  narrative: string
  evidenceRefs: string[]
}

/** The full reconciliation result for one issuer. */
export interface DossierResponse {
  id: string
  issuerId: string
  /** ISO 8601 date-time (as-of the sweep). */
  asOf: string
  verdicts: ProviderVerdictDto[]
  divergences: PairDivergenceDto[]
  flags: RedFlagDto[]
  consensusSummary: string
  /** 0..1, deterministic from coverage + freshness. */
  confidence: number
}

/** Request body for POST /api/v1/reconciliations. */
export interface ReconciliationRequest {
  issuerId: string
  /** ISO 8601 date-time. */
  asOf: string
  /** null / omitted = all covered providers. */
  providers?: Provider[]
}

/** Standard error envelope for every non-2xx response (arch-03). */
export interface ApiErrorBody {
  error: {
    code: string
    message: string
    details?: unknown
  }
}

// ── Live market-context enrichment (Morningstar) ────────────────────────────────────────
// A CONTEXT-ONLY side feature (live Morningstar analyst research for a real listed security),
// deliberately separate from the rating reconciliation above and never a buy/sell/hold view (P4).
// Mirrors Models/MarketContextDtos.cs. Every status is a 200 the panel can render (fail-soft).

/** Outcome of a live Morningstar lookup (C# `MarketContextStatus` member names). */
export type MarketContextStatus = 'Ok' | 'NotCovered' | 'Disabled' | 'ReloginRequired' | 'Unavailable'

/** The security Morningstar matched an identifier to. */
export interface MarketContextInvestment {
  morningstarId: string
  name: string
  ticker: string | null
  investmentType: string | null
  exchange: string | null
}

/** One attributed Morningstar analyst-research section. */
export interface MarketContextSection {
  title: string
  /** ISO 8601 date-time, or null. */
  publishedAt: string | null
  excerpt: string
  url: string | null
}

/** Payload for the live-Morningstar context panel (GET /api/v1/market-context/morningstar). */
export interface MorningstarContextResponse {
  identifier: string
  status: MarketContextStatus
  message: string
  investment: MarketContextInvestment | null
  sections: MarketContextSection[]
  sourceProvider: string
  /** ISO 8601 date-time when retrieved, or null for non-Ok states. */
  retrievedAt: string | null
  disclaimer: string
}
