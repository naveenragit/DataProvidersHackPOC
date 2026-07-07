import { describe, it, expect } from 'vitest'
import {
  formatConfidence,
  formatUtcDate,
  highSeverityFlags,
  isResidualDominated,
  residualShare,
  sortFlagsBySeverity,
  widestDivergence,
} from './prismFormat'
import { nordstarDossier, onyxDossier, richDivergence } from '@/test/prismFixtures'

describe('prismFormat', () => {
  describe('formatUtcDate', () => {
    it("formats a DateTimeOffset to the UTC calendar date matching the rule text", () => {
      // Msci input 2025-09-14T20:00:00-04:00 == 2025-09-15 UTC (the STALE rule says 2025-09-15).
      expect(formatUtcDate('2025-09-14T20:00:00-04:00')).toBe('2025-09-15')
    })

    it('returns the raw input unchanged when it is not a valid date', () => {
      expect(formatUtcDate('not-a-date')).toBe('not-a-date')
    })
  })

  describe('formatConfidence', () => {
    it('renders a 0..1 score as a whole percent', () => {
      expect(formatConfidence(0.6666666666666667)).toBe('67%')
      expect(formatConfidence(1)).toBe('100%')
      expect(formatConfidence(0.55)).toBe('55%')
    })
  })

  describe('residualShare / isResidualDominated', () => {
    it('flags a letter-only pair (whole gap is methodology residual) as residual-dominated', () => {
      const nordstarMsci = nordstarDossier.divergences.find((d) => d.b === 'Msci' && d.a === 'Moodys')!
      expect(residualShare(nordstarMsci)).toBe(1)
      expect(isResidualDominated(nordstarMsci)).toBe(true)
    })

    it('does NOT flag a rich pair (weighting + input non-trivial) as residual-dominated', () => {
      expect(residualShare(richDivergence)).toBeCloseTo(1 / 3, 5)
      expect(isResidualDominated(richDivergence)).toBe(false)
    })

    it('treats a 0-notch gap as not residual-dominated', () => {
      const consensusPair = nordstarDossier.divergences.find((d) => d.notchGap === 0)!
      expect(isResidualDominated(consensusPair)).toBe(false)
    })
  })

  describe('widestDivergence', () => {
    it('returns the pair with the widest absolute notch gap', () => {
      const widest = widestDivergence(onyxDossier.divergences)
      expect(widest?.notchGap).toBe(4)
    })

    it('returns undefined for an empty list', () => {
      expect(widestDivergence([])).toBeUndefined()
    })
  })

  describe('flag ordering', () => {
    it('orders flags high → medium → low', () => {
      const mixed = [
        { code: 'MISSING_COVERAGE' as const, severity: 'medium' as const, rule: '', narrative: '', evidenceRefs: [] },
        { code: 'STALE_INPUT' as const, severity: 'high' as const, rule: '', narrative: '', evidenceRefs: [] },
        { code: 'OUTLIER_PROVIDER' as const, severity: 'low' as const, rule: '', narrative: '', evidenceRefs: [] },
      ]
      expect(sortFlagsBySeverity(mixed).map((f) => f.severity)).toEqual(['high', 'medium', 'low'])
    })

    it('highSeverityFlags keeps only high-severity flags', () => {
      expect(highSeverityFlags(nordstarDossier.flags)).toHaveLength(1)
      expect(highSeverityFlags(onyxDossier.flags)).toHaveLength(0)
    })
  })
})
