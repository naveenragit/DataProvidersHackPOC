// frontend/src/lib/evidenceCatalog.ts
//
// Two presentation-only helpers that make the reconciliation's rules + evidence useful for a
// decision-maker:
//
//   1. describeEvidenceRef(ref)  — turns a terse internal evidence id (e.g. "nordstar-Msci",
//      "edgar:0000040545:10-Q", "moodys-methodology") into a readable label + one-line detail.
//
//   2. precedentsForFlag(code) / precedentForBucket(bucket) — maps each deterministic divergence
//      type to REAL, documented historical rating-divergence cases. These are research-grounded
//      analogues (why agencies actually disagree), clearly shown as precedents — NOT claims about
//      this (illustrative-rating) issuer, and never a buy/sell/hold view (P4).
//
// Sources (public record): S&P/Moody's/Fitch rating actions; SEC NRSRO staff reports; IMF Global
// Financial Stability Report (Oct 2010) on how agencies measure default probability vs expected loss.

import type { Bucket, RedFlagCode } from '@/types/prism'

export interface EvidenceRow {
  label: string
  detail: string
}

export interface Precedent {
  /** Short case title, e.g. "Kraft Heinz — split across the IG/HY line". */
  title: string
  /** When it happened, e.g. "Feb 2020". */
  period: string
  /** What happened and why the agencies diverged. */
  detail: string
  /** Where it is on the public record. */
  source: string
}

const PROVIDER_LABEL: Record<string, string> = {
  Moodys: "Moody's",
  Msci: 'MSCI',
  MorningstarDbrs: 'Morningstar DBRS',
  moodys: "Moody's",
  msci: 'MSCI',
  morningstardbrs: 'Morningstar DBRS',
}

/** Resolve a deterministic evidence ref id to a human-readable row. */
export function describeEvidenceRef(ref: string): EvidenceRow {
  // SEC EDGAR filing: "edgar:{cik}:{filingType}"
  const edgar = /^edgar:([0-9]+):(.+)$/.exec(ref)
  if (edgar) {
    const [, cik, filingType] = edgar
    return {
      label: `SEC EDGAR filing (${filingType})`,
      detail: `Point-in-time issuer fundamentals from the SEC EDGAR ${filingType} filing (CIK ${cik}) — the as-of boundary freshness is measured against.`,
    }
  }

  // Methodology doc: "{provider}-methodology"
  const methodology = /^([a-z]+)-methodology$/.exec(ref)
  if (methodology && PROVIDER_LABEL[methodology[1]]) {
    const provider = PROVIDER_LABEL[methodology[1]]
    return {
      label: `${provider} methodology`,
      detail: `The ${provider} methodology summary — the factor weights and adjustments (e.g. parent-support uplift) behind its assessment.`,
    }
  }

  // Quarterly fundamentals citation: "{issuer}-fin-q3" / "-q2"
  const fin = /-fin-(q[0-9])$/i.exec(ref)
  if (fin) {
    const quarter = fin[1].toUpperCase()
    return {
      label: `Issuer ${quarter} fundamentals`,
      detail: `The issuer's ${quarter} financial inputs (leverage, interest coverage) that this provider's assessment rests on.`,
    }
  }

  // Rating card: "{issuer}-{Provider}" (provider capitalised, e.g. "nordstar-Msci")
  const card = /-(Moodys|Msci|MorningstarDbrs)$/.exec(ref)
  if (card) {
    const provider = PROVIDER_LABEL[card[1]]
    return {
      label: `${provider} rating card`,
      detail: `The ${provider} rating card for this issuer — its letter, notch, factor scores, and rating/input as-of dates.`,
    }
  }

  return { label: ref, detail: 'Source document reference behind this rule.' }
}

// ── Real-world precedents, per deterministic flag type ──────────────────────────────────
// Curated from the public record. Each is a documented case where agencies rated the SAME issuer
// differently for the reason this flag captures.

const FLAG_PRECEDENTS: Record<RedFlagCode, Precedent[]> = {
  STALE_INPUT: [
    {
      title: 'U.S. sovereign — a 14-year split driven by timing',
      period: '2011 – 2025',
      detail:
        'S&P cut the U.S. to AA+ on 5 Aug 2011; Fitch held AAA until it also cut to AA+ on 1 Aug 2023; Moody\u2019s held Aaa until it moved to Aa1 on 16 May 2025. The same issuer sat at different levels for over a decade largely because each agency acted at a different time.',
      source: 'S&P (2011), Fitch (2023), Moody\u2019s (2025) rating actions',
    },
    {
      title: 'Markets re-price before agencies re-rate',
      period: 'documented pattern',
      detail:
        'Empirical studies find widening bond yield spreads typically precede agency downgrades — a rating that has not yet incorporated the latest filing can be stale relative to the fundamentals the market is already pricing.',
      source: 'Financial-economics literature on ratings timeliness',
    },
  ],
  IG_HY_BOUNDARY: [
    {
      title: 'Kraft Heinz — a split straddling the investment-grade line',
      period: 'Feb 2020',
      detail:
        'S&P and Fitch moved Kraft Heinz to BB+ (high yield) while Moody\u2019s kept it at Baa3 (lowest investment grade). Because index rules take the middle of three ratings, the bonds stayed in investment-grade indices — a vivid case of how a one-notch split across this line decides index membership.',
      source: 'S&P / Fitch / Moody\u2019s rating actions, Feb 2020',
    },
    {
      title: 'The 2020 fallen-angel wave — index migration at the boundary',
      period: 'Mar\u2013May 2020',
      detail:
        'Ford, Occidental, Macy\u2019s and Kraft Heinz together drove a record volume of \u201cfallen angels\u201d as ratings crossed BBB-/Baa3 into high yield, forcing bonds out of investment-grade indices (e.g. the Bloomberg US Aggregate) and into high-yield indices — with knock-on effects on eligible investors and spreads.',
      source: 'Rating actions and index-methodology rules, 2020',
    },
    {
      title: 'Why the line matters — capital, indices, collateral',
      period: 'structural',
      detail:
        'Investment-grade vs high-yield status drives NAIC and Solvency II capital charges, index inclusion, and repo / central-bank collateral eligibility. A single-notch disagreement at BBB-/Baa3 changes which rulebook applies, so it is the most consequential divergence Prism surfaces.',
      source: 'NAIC / Solvency II capital frameworks; index and collateral rules',
    },
  ],
  MISSING_COVERAGE: [
    {
      title: 'Coverage is concentrated and not universal',
      period: 'ongoing',
      detail:
        'The \u201cBig Three\u201d (S&P ~50%, Moody\u2019s ~32%, Fitch ~12%) issue ~94% of all ratings, and DBRS Morningstar / MSCI cover different issuer sets. Many issuers carry only one or two ratings, so there are fewer independent opinions to triangulate.',
      source: 'SEC NRSRO staff reports; industry market-share data',
    },
    {
      title: 'Unsolicited ratings skew conservative',
      period: 'documented pattern',
      detail:
        'Academic and regulatory reviews find unsolicited ratings (assigned without issuer-supplied information) tend to run lower than solicited ones — a coverage gap is not a neutral omission, and confidence should fall when opinions are missing.',
      source: 'Studies on solicited vs unsolicited rating bias',
    },
  ],
  OUTLIER_PROVIDER: [
    {
      title: 'Default-probability vs expected-loss scales',
      period: 'structural',
      detail:
        'S&P rates to the probability of default, while Moody\u2019s and Fitch incorporate expected loss (they credit recovery). On the same issuer this alone can push one agency several notches from the others, especially where seniority or recovery differs.',
      source: 'Agency methodologies; IMF Global Financial Stability Report, Oct 2010',
    },
    {
      title: 'Tesla — investment grade at one agency, high yield at another',
      period: '2022 – 2023',
      detail:
        'S&P raised Tesla to investment grade (BBB) in Oct 2022 while Moody\u2019s kept it in high yield (Ba1) into 2023 — a multi-notch gap driven by differing judgments on the durability of the business rather than by the financials.',
      source: 'S&P (Oct 2022), Moody\u2019s (2023) rating actions',
    },
  ],
  PROVIDER_UNDER_REVIEW: [
    {
      title: 'CreditWatch / Review resolves quickly — and usually as flagged',
      period: 'structural',
      detail:
        'S&P states a CreditWatch is typically resolved within about 90 days, and Moody\u2019s reviews run to a similar horizon. A rating placed under review is an explicit notice that a change is likely in the near term, so a divergence involving it is often transient — the reviewed opinion tends to move toward the peer consensus.',
      source: 'Agency watch / review procedures',
    },
  ],
  METHODOLOGY_CONFLICT: [
    {
      title: 'Kraft Heinz — split across the IG/HY line',
      period: 'Feb 2020',
      detail:
        'S&P and Fitch moved Kraft Heinz to BB+ (high yield) while Moody\u2019s kept it at Baa3 (lowest investment grade). Same fundamentals, different conclusions — the split reflected differing views on deleveraging and financial policy, not new data.',
      source: 'S&P / Fitch / Moody\u2019s rating actions, Feb 2020',
    },
    {
      title: 'Ford — a fallen angel re-rated at different times',
      period: '2019 – 2023',
      detail:
        'Moody\u2019s cut Ford to junk (Ba1) in Sept 2019; S&P and Fitch followed to BB+ in 2020; the agencies then restored investment grade at different times in 2023. Structural and methodology differences moved it across the IG/HY line at different moments.',
      source: 'Moody\u2019s (2019), S&P / Fitch (2020, 2023) rating actions',
    },
    {
      title: 'Bank holding companies — support and structural notching',
      period: 'post-2015',
      detail:
        'After Dodd-Frank, agencies removed \u201ctoo-big-to-fail\u201d government-support uplift from large-bank ratings at different times, and they notch a holding company below its operating subsidiaries by different amounts — a persistent methodology-driven gap.',
      source: 'Post-2015 bank rating methodology changes',
    },
  ],
}

/** Real historical precedents for a red-flag type (why agencies actually diverge this way). */
export function precedentsForFlag(code: RedFlagCode): Precedent[] {
  return FLAG_PRECEDENTS[code] ?? []
}

const BUCKET_PRECEDENT: Record<Bucket, Precedent> = {
  Weighting: {
    title: 'Same metrics, different weights',
    period: 'structural',
    detail:
      'Agencies weight the same factors differently — S&P builds a rating from a business-risk \u00d7 financial-risk matrix, while Moody\u2019s scores a factor grid. Identical leverage and coverage can therefore rank differently.',
    source: 'Agency corporate rating methodologies',
  },
  Input: {
    title: 'Same issuer, different input vintage',
    period: 'documented pattern',
    detail:
      'One agency may still be rating off an older quarter. Rating lag is the classic driver — see the U.S. sovereign, where S&P (2011), Fitch (2023) and Moody\u2019s (2025) acted years apart on the same borrower.',
    source: 'S&P / Fitch / Moody\u2019s sovereign actions, 2011\u20132025',
  },
  MethodologyAdjustment: {
    title: 'Judgment-driven residual (parent support, subordination, ESG)',
    period: 'structural',
    detail:
      'The residual is where parent/group-support uplift, structural subordination, government-support assumptions and ESG overlays live. Precedent: Kraft Heinz (Feb 2020), split across the investment-grade line on the same fundamentals.',
    source: 'S&P / Fitch / Moody\u2019s actions, Feb 2020',
  },
}

/** The real-world reason a given decomposition bucket carries mass. */
export function precedentForBucket(bucket: Bucket): Precedent {
  return BUCKET_PRECEDENT[bucket]
}
