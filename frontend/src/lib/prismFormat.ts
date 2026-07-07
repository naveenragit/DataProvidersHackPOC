// frontend/src/lib/prismFormat.ts
//
// Pure presentation helpers for the Prism reconciliation UI. These are DISPLAY DERIVATIONS
// over values the API already computed — they NEVER compute a notch, gap, or red flag (P2).
// The deterministic core (backend Analysis/*) owns that math; here we only decide how to
// render it: format dates/percentages, pick which pair to highlight, and choose which chart
// treatment is honest for a given (already-computed) attribution.
import type { Bucket, PairDivergenceDto, RedFlagDto, Severity } from '@/types/prism'

/**
 * Formats an ISO 8601 date-time (C# DateTimeOffset, e.g. "2025-09-14T20:00:00-04:00") to a
 * yyyy-MM-dd string in **UTC**. The backend's deterministic rule text uses the UTC calendar
 * date (that Msci instant is 2025-09-15 UTC — the STALE rule says "2025-09-15"), so formatting
 * verdict dates in UTC keeps the board consistent with the rule text. Invalid input is returned
 * unchanged (fail-soft on display only; never fabricates a date).
 */
export function formatUtcDate(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return d.toISOString().slice(0, 10)
}

/** Formats a 0..1 confidence score as a whole-percent string (0.6667 → "67%"). */
export function formatConfidence(confidence: number): string {
  return `${Math.round(confidence * 100)}%`
}

/** Returns the attribution entry for a bucket within a decomposed divergence (or undefined). */
export function bucketOf(
  divergence: PairDivergenceDto,
  bucket: Bucket,
): PairDivergenceDto['attribution'][number] | undefined {
  return divergence.attribution.find((a) => a.bucket === bucket)
}

/**
 * Share of the (already-computed) notch gap absorbed by the residual MethodologyAdjustment
 * bucket: |methodology notches| / max(|gap|, 1). Mirrors the backend
 * `DivergenceDecomposer.ResidualShare` (pkg 05) but is used ONLY to choose the honest chart
 * treatment — it does not re-derive the gap.
 */
export function residualShare(divergence: PairDivergenceDto): number {
  const methodology = bucketOf(divergence, 'MethodologyAdjustment')?.notches ?? 0
  return Math.abs(methodology) / Math.max(Math.abs(divergence.notchGap), 1)
}

/**
 * True when the notch gap is ≥1 and (near-)entirely a methodology residual — i.e. Weighting and
 * Input explain little of it. Letter-only real providers land here (pkg-05 product truth), so the
 * UI leads with the red flag + a "methodology-driven divergence" framing instead of a misleading
 * rich waterfall. Presentation decision only (P2).
 */
export function isResidualDominated(divergence: PairDivergenceDto, threshold = 0.8): boolean {
  return Math.abs(divergence.notchGap) >= 1 && residualShare(divergence) >= threshold
}

/** The divergence with the widest absolute notch gap (highlight pick), or undefined if none. */
export function widestDivergence(
  divergences: PairDivergenceDto[],
): PairDivergenceDto | undefined {
  if (divergences.length === 0) return undefined
  return divergences.reduce((widest, d) =>
    Math.abs(d.notchGap) > Math.abs(widest.notchGap) ? d : widest,
  )
}

const SEVERITY_RANK: Record<Severity, number> = { high: 0, medium: 1, low: 2 }

/** Red flags whose severity is "high" (the money-moment banner is gated on this). */
export function highSeverityFlags(flags: RedFlagDto[]): RedFlagDto[] {
  return flags.filter((f) => f.severity === 'high')
}

/** Returns a new array of flags ordered high → medium → low (stable within a severity). */
export function sortFlagsBySeverity(flags: RedFlagDto[]): RedFlagDto[] {
  return [...flags].sort(
    (a, b) => (SEVERITY_RANK[a.severity] ?? 99) - (SEVERITY_RANK[b.severity] ?? 99),
  )
}
