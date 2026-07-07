import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ProviderVerdictCard } from '../ProviderVerdictCard'
import { nordstarDossier } from '@/test/prismFixtures'

describe('ProviderVerdictCard', () => {
  const msci = nordstarDossier.verdicts.find((v) => v.provider === 'Msci')!

  it('renders the provider label, native letter, and notch', () => {
    render(<ProviderVerdictCard verdict={msci} />)
    expect(screen.getByText('MSCI')).toBeInTheDocument()
    expect(screen.getByText('BBB-')).toBeInTheDocument()
    expect(screen.getByText('notch 10')).toBeInTheDocument()
  })

  it('formats the as-of dates in UTC (consistent with the rule text) and cites the methodology doc', () => {
    render(<ProviderVerdictCard verdict={msci} />)
    // 2025-09-14T20:00:00-04:00 → 2025-09-15 UTC
    expect(screen.getAllByText('2025-09-15').length).toBeGreaterThan(0)
    expect(screen.getByText('nordstar-msci')).toBeInTheDocument()
  })
})
