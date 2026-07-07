import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'

// Mock the reconciliation hook so the page renders a known dossier without network / providers.
vi.mock('@/hooks/useReconciliation')

import ReconciliationPage from '../ReconciliationPage'
import { useReconciliationRun } from '@/hooks/useReconciliation'
import { nordstarDossier } from '@/test/prismFixtures'

function mockRun(overrides: Record<string, unknown>) {
  vi.mocked(useReconciliationRun).mockReturnValue({
    refetch: vi.fn(),
    data: undefined,
    error: null,
    isPending: false,
    isFetching: false,
    isError: false,
    ...overrides,
  } as unknown as ReturnType<typeof useReconciliationRun>)
}

function renderPage(issuer = 'nordstar') {
  return render(
    <MemoryRouter initialEntries={[`/reconciliation?issuer=${issuer}`]}>
      <ReconciliationPage />
    </MemoryRouter>,
  )
}

describe('ReconciliationPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders the NordStar dossier with the deterministic STALE rule leading (money moment)', () => {
    mockRun({ data: nordstarDossier })
    renderPage('nordstar')

    // Issuer header + scope notice.
    expect(screen.getAllByText('nordstar').length).toBeGreaterThan(0)
    expect(screen.getByTestId('scope-notice')).toBeInTheDocument()

    // The high-severity banner leads, and the verbatim rule appears (banner + panel).
    expect(screen.getByTestId('red-flag-banner')).toBeInTheDocument()
    expect(
      screen.getAllByText(
        "Rating action dated 2025-09-15 predates the issuer's latest filing (10-Q) on 2025-11-05.",
      ).length,
    ).toBeGreaterThan(0)

    // Board + dossier export are present.
    expect(screen.getByTestId('divergence-board')).toBeInTheDocument()
    expect(screen.getByTestId('dossier-panel')).toBeInTheDocument()
  })

  it('shows an honest loading state while the reconciliation runs', () => {
    mockRun({ isPending: true })
    renderPage('nordstar')
    expect(screen.getByText(/Reconciling provider ratings/)).toBeInTheDocument()
  })

  it('shows an honest error state (no fabricated results) on failure', () => {
    mockRun({ isError: true, error: null })
    renderPage('nordstar')
    expect(screen.getByText('Reconciliation failed')).toBeInTheDocument()
    expect(screen.queryByTestId('divergence-board')).not.toBeInTheDocument()
  })

  it('prompts to pick an issuer when none is selected', () => {
    mockRun({})
    renderPage('')
    expect(screen.getByText('Pick an issuer to reconcile')).toBeInTheDocument()
  })
})
