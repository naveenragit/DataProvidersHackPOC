import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { RedFlagPanel } from '../RedFlagPanel'
import { nordstarDossier, cedarGroveDossier, onyxDossier } from '@/test/prismFixtures'

describe('RedFlagPanel', () => {
  it('renders the verbatim STALE_INPUT rule text and its cited evidence refs', () => {
    render(<RedFlagPanel flags={nordstarDossier.flags} />)
    expect(
      screen.getByText(
        "Rating action dated 2025-09-15 predates the issuer's latest filing (10-Q) on 2025-11-05.",
      ),
    ).toBeInTheDocument()
    // Both dated source rows are shown as evidence chips.
    expect(screen.getByText('nordstar-Msci')).toBeInTheDocument()
    expect(screen.getByText('edgar:0000000001:10-Q')).toBeInTheDocument()
  })

  it('shows the honest consensus state when there are no flags', () => {
    render(<RedFlagPanel flags={cedarGroveDossier.flags} />)
    expect(
      screen.getByText(/No red flags — provider ratings reconcile; fully defensible\./),
    ).toBeInTheDocument()
  })

  it('lists every flag (outlier + methodology conflicts) for a multi-flag dossier', () => {
    render(<RedFlagPanel flags={onyxDossier.flags} />)
    expect(screen.getAllByTestId('red-flag-row')).toHaveLength(3)
  })
})
