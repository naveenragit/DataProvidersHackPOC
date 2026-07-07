import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { DivergenceBoard } from '../DivergenceBoard'
import { nordstarDossier, asterBioDossier } from '@/test/prismFixtures'

describe('DivergenceBoard', () => {
  it('renders the deterministic consensus summary and a verdict card per covered provider', () => {
    render(<DivergenceBoard dossier={nordstarDossier} />)
    expect(screen.getByText('consensus within 1 notch')).toBeInTheDocument()
    expect(screen.getAllByTestId('provider-verdict-card')).toHaveLength(3)
    expect(screen.queryByTestId('missing-coverage-card')).not.toBeInTheDocument()
  })

  it('renders an explicit missing-coverage slot when a provider does not cover the issuer', () => {
    render(<DivergenceBoard dossier={asterBioDossier} />)
    expect(screen.getAllByTestId('provider-verdict-card')).toHaveLength(2)
    const missing = screen.getByTestId('missing-coverage-card')
    expect(missing).toHaveAttribute('data-provider', 'Msci')
    expect(screen.getByText('No rating published')).toBeInTheDocument()
  })
})
