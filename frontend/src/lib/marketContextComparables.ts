// frontend/src/lib/marketContextComparables.ts
// Curated, presentation-only mapping from a Prism issuer's SECTOR to a real, listed, Morningstar-
// covered company shown purely as market CONTEXT for that sector. Prism's issuers are illustrative
// (fictional) and never resolve in Morningstar's real-security universe, so the Market Context panel
// surfaces live analyst research for a *comparable real-world name in the same sector* — explicitly
// not the issuer itself, and never a buy/sell/hold view (P4). This is an editorial choice for the
// demo only; it touches nothing in the deterministic reconciliation core (P2).

export interface SectorComparable {
  /** A real listed ticker Morningstar covers with equity analyst research (moat / fair value / bulls-bears). */
  ticker: string
  /** Human-friendly company name, used for labeling. */
  name: string
}

/**
 * Prism issuer sector (as returned by GET /api/v1/issuers) → a well-known real comparable in that
 * sector. Each name is a large, broadly-covered US issuer that Morningstar publishes analyst research
 * on, chosen to echo the illustrative issuer's sub-theme (e.g. "Freight" → a rail/freight bellwether).
 */
const BY_SECTOR: Record<string, SectorComparable> = {
  Industrials: { ticker: 'CAT', name: 'Caterpillar Inc.' },
  Utilities: { ticker: 'DUK', name: 'Duke Energy Corporation' },
  Healthcare: { ticker: 'AMGN', name: 'Amgen Inc.' },
  Financials: { ticker: 'BLK', name: 'BlackRock, Inc.' },
  Transportation: { ticker: 'UNP', name: 'Union Pacific Corporation' },
  'Consumer Staples': { ticker: 'MDLZ', name: 'Mondelez International, Inc.' },
}

/** Returns the curated real comparable for a sector, or null when the sector isn't mapped. */
export function comparableForSector(sector: string | null | undefined): SectorComparable | null {
  if (!sector) return null
  return BY_SECTOR[sector] ?? null
}
