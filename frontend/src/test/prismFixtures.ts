// frontend/src/test/prismFixtures.ts
// Test-only fixtures mirroring REAL dossiers captured from the live API (GET/POST
// /api/v1/reconciliations on 2026-07-06). Used solely by vitest — never imported by runtime code
// (P1: mocks live only in tests). Values are reproduced verbatim from the running backend.
import type { DossierResponse, PairDivergenceDto } from '@/types/prism'

/** NordStar — the STALE money moment. 9/9/10, all divergences residual-dominated. */
export const nordstarDossier: DossierResponse = {
  id: 'nordstar:3a461174b96d4b848035b86747ef9e55',
  issuerId: 'nordstar',
  asOf: '2026-07-06T00:00:00-04:00',
  verdicts: [
    {
      provider: 'Moodys',
      letter: 'Baa2',
      notch: 9,
      asOfDate: '2025-11-11T19:00:00-05:00',
      inputAsOfDate: '2025-11-11T19:00:00-05:00',
      methodologyDocId: 'nordstar-moodys',
      outlook: 'Unknown',
      underReview: false,
    },
    {
      provider: 'MorningstarDbrs',
      letter: 'BBB (mid)',
      notch: 9,
      asOfDate: '2025-11-13T19:00:00-05:00',
      inputAsOfDate: '2025-11-13T19:00:00-05:00',
      methodologyDocId: 'nordstar-morningstardbrs',
      outlook: 'Unknown',
      underReview: false,
    },
    {
      provider: 'Msci',
      letter: 'BBB-',
      notch: 10,
      asOfDate: '2025-09-14T20:00:00-04:00',
      inputAsOfDate: '2025-09-14T20:00:00-04:00',
      methodologyDocId: 'nordstar-msci',
      outlook: 'Unknown',
      underReview: false,
    },
  ],
  divergences: [
    {
      a: 'Moodys',
      b: 'MorningstarDbrs',
      notchGap: 0,
      attribution: [
        { bucket: 'Weighting', notches: 0, explanation: '', evidenceRefs: ['nordstar-moodys', 'nordstar-morningstardbrs'] },
        { bucket: 'Input', notches: 0, explanation: '', evidenceRefs: [] },
        { bucket: 'MethodologyAdjustment', notches: 0, explanation: '', evidenceRefs: [] },
      ],
    },
    {
      a: 'Moodys',
      b: 'Msci',
      notchGap: 1,
      attribution: [
        { bucket: 'Weighting', notches: 0, explanation: '', evidenceRefs: ['nordstar-moodys', 'nordstar-msci'] },
        { bucket: 'Input', notches: 0, explanation: '', evidenceRefs: [] },
        { bucket: 'MethodologyAdjustment', notches: 1, explanation: '', evidenceRefs: [] },
      ],
    },
    {
      a: 'MorningstarDbrs',
      b: 'Msci',
      notchGap: 1,
      attribution: [
        { bucket: 'Weighting', notches: 0, explanation: '', evidenceRefs: ['nordstar-morningstardbrs', 'nordstar-msci'] },
        { bucket: 'Input', notches: 0, explanation: '', evidenceRefs: [] },
        { bucket: 'MethodologyAdjustment', notches: 1, explanation: '', evidenceRefs: [] },
      ],
    },
  ],
  flags: [
    {
      code: 'STALE_INPUT',
      severity: 'high',
      rule: "Rating action dated 2025-09-15 predates the issuer's latest filing (10-Q) on 2025-11-05.",
      narrative: '',
      evidenceRefs: ['nordstar-Msci', 'edgar:0000000001:10-Q'],
    },
  ],
  consensusSummary: 'consensus within 1 notch',
  confidence: 0.7,
}

/** Cedar Grove — the all-green consensus path. */
export const cedarGroveDossier: DossierResponse = {
  id: 'cedargrove:consensustestfixture0000000000000000',
  issuerId: 'cedargrove',
  asOf: '2026-07-06T00:00:00-04:00',
  verdicts: [
    { provider: 'Moodys', letter: 'A2', notch: 6, asOfDate: '2025-11-11T19:00:00-05:00', inputAsOfDate: '2025-11-11T19:00:00-05:00', methodologyDocId: 'cedargrove-moodys', outlook: 'Unknown', underReview: false },
    { provider: 'MorningstarDbrs', letter: 'A (mid)', notch: 6, asOfDate: '2025-11-13T19:00:00-05:00', inputAsOfDate: '2025-11-13T19:00:00-05:00', methodologyDocId: 'cedargrove-morningstardbrs', outlook: 'Unknown', underReview: false },
    { provider: 'Msci', letter: 'A', notch: 6, asOfDate: '2025-11-10T19:00:00-05:00', inputAsOfDate: '2025-11-10T19:00:00-05:00', methodologyDocId: 'cedargrove-msci', outlook: 'Unknown', underReview: false },
  ],
  divergences: [
    { a: 'Moodys', b: 'MorningstarDbrs', notchGap: 0, attribution: [
      { bucket: 'Weighting', notches: 0, explanation: '', evidenceRefs: [] },
      { bucket: 'Input', notches: 0, explanation: '', evidenceRefs: [] },
      { bucket: 'MethodologyAdjustment', notches: 0, explanation: '', evidenceRefs: [] },
    ] },
  ],
  flags: [],
  consensusSummary: 'full consensus',
  confidence: 1,
}

/** Aster Bio — missing coverage (Msci absent → 2 verdicts + a MISSING_COVERAGE flag). */
export const asterBioDossier: DossierResponse = {
  id: 'asterbio:missingcoveragefixture000000000000000000',
  issuerId: 'asterbio',
  asOf: '2026-07-06T00:00:00-04:00',
  verdicts: [
    { provider: 'Moodys', letter: 'Ba2', notch: 12, asOfDate: '2025-11-11T19:00:00-05:00', inputAsOfDate: '2025-11-11T19:00:00-05:00', methodologyDocId: 'asterbio-moodys', outlook: 'Unknown', underReview: false },
    { provider: 'MorningstarDbrs', letter: 'BB (mid)', notch: 12, asOfDate: '2025-11-13T19:00:00-05:00', inputAsOfDate: '2025-11-13T19:00:00-05:00', methodologyDocId: 'asterbio-morningstardbrs', outlook: 'Unknown', underReview: false },
  ],
  divergences: [
    { a: 'Moodys', b: 'MorningstarDbrs', notchGap: 0, attribution: [
      { bucket: 'Weighting', notches: 0, explanation: '', evidenceRefs: [] },
      { bucket: 'Input', notches: 0, explanation: '', evidenceRefs: [] },
      { bucket: 'MethodologyAdjustment', notches: 0, explanation: '', evidenceRefs: [] },
    ] },
  ],
  flags: [
    { code: 'MISSING_COVERAGE', severity: 'medium', rule: 'Msci publishes no rating for Aster Bio Therapeutics Inc..', narrative: '', evidenceRefs: [] },
  ],
  consensusSummary: 'full consensus',
  confidence: 0.6666666666666667,
}

/** Onyx — outlier + 4-notch split (all residual-dominated). */
export const onyxDossier: DossierResponse = {
  id: 'onyx:outlierfixture00000000000000000000000000000',
  issuerId: 'onyx',
  asOf: '2026-07-06T00:00:00-04:00',
  verdicts: [
    { provider: 'Moodys', letter: 'A2', notch: 6, asOfDate: '2025-11-11T19:00:00-05:00', inputAsOfDate: '2025-11-11T19:00:00-05:00', methodologyDocId: 'onyx-moodys', outlook: 'Unknown', underReview: false },
    { provider: 'MorningstarDbrs', letter: 'A (mid)', notch: 6, asOfDate: '2025-11-13T19:00:00-05:00', inputAsOfDate: '2025-11-13T19:00:00-05:00', methodologyDocId: 'onyx-morningstardbrs', outlook: 'Unknown', underReview: false },
    { provider: 'Msci', letter: 'BBB-', notch: 10, asOfDate: '2025-11-10T19:00:00-05:00', inputAsOfDate: '2025-11-10T19:00:00-05:00', methodologyDocId: 'onyx-msci', outlook: 'Unknown', underReview: false },
  ],
  divergences: [
    { a: 'Moodys', b: 'MorningstarDbrs', notchGap: 0, attribution: [
      { bucket: 'Weighting', notches: 0, explanation: '', evidenceRefs: [] },
      { bucket: 'Input', notches: 0, explanation: '', evidenceRefs: [] },
      { bucket: 'MethodologyAdjustment', notches: 0, explanation: '', evidenceRefs: [] },
    ] },
    { a: 'Moodys', b: 'Msci', notchGap: 4, attribution: [
      { bucket: 'Weighting', notches: 0, explanation: '', evidenceRefs: ['onyx-moodys', 'onyx-msci'] },
      { bucket: 'Input', notches: 0, explanation: '', evidenceRefs: [] },
      { bucket: 'MethodologyAdjustment', notches: 4, explanation: '', evidenceRefs: [] },
    ] },
    { a: 'MorningstarDbrs', b: 'Msci', notchGap: 4, attribution: [
      { bucket: 'Weighting', notches: 0, explanation: '', evidenceRefs: ['onyx-morningstardbrs', 'onyx-msci'] },
      { bucket: 'Input', notches: 0, explanation: '', evidenceRefs: [] },
      { bucket: 'MethodologyAdjustment', notches: 4, explanation: '', evidenceRefs: [] },
    ] },
  ],
  flags: [
    { code: 'OUTLIER_PROVIDER', severity: 'medium', rule: 'Msci sits 4 notches from the peer median.', narrative: '', evidenceRefs: ['onyx-Msci'] },
    { code: 'METHODOLOGY_CONFLICT', severity: 'medium', rule: 'Moodys vs Msci differ by 4 notches; 100% of the gap is a methodology residual not mechanically attributable to factor weighting or input timing.', narrative: '', evidenceRefs: ['onyx-moodys', 'onyx-msci'] },
    { code: 'METHODOLOGY_CONFLICT', severity: 'medium', rule: 'MorningstarDbrs vs Msci differ by 4 notches; 100% of the gap is a methodology residual not mechanically attributable to factor weighting or input timing.', narrative: '', evidenceRefs: ['onyx-morningstardbrs', 'onyx-msci'] },
  ],
  consensusSummary: '4-notch split',
  confidence: 0.55,
}

/**
 * Synthetic RICH divergence (test-only, clearly not from the API). Weighting + Input are
 * non-trivial so residualShare = 1/3 < 0.8 → NOT residual-dominated → exercises the recharts
 * waterfall branch. Constructed to verify the component, not a real reconciliation output.
 */
export const richDivergence: PairDivergenceDto = {
  a: 'Moodys',
  b: 'Msci',
  notchGap: 3,
  attribution: [
    { bucket: 'Weighting', notches: 1, explanation: '', evidenceRefs: ['x-moodys', 'x-msci'] },
    { bucket: 'Input', notches: 1, explanation: '', evidenceRefs: [] },
    { bucket: 'MethodologyAdjustment', notches: 1, explanation: '', evidenceRefs: [] },
  ],
}
