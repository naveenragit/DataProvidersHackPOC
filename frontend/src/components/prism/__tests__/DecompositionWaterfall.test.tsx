import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { DecompositionWaterfall } from '../DecompositionWaterfall'
import { nordstarDossier, richDivergence } from '@/test/prismFixtures'

describe('DecompositionWaterfall', () => {
  const residualPair = nordstarDossier.divergences.find((d) => d.a === 'Moodys' && d.b === 'Msci')!
  const consensusPair = nordstarDossier.divergences.find((d) => d.notchGap === 0)!

  it('leads with the honest methodology-driven framing for a residual-dominated gap', () => {
    render(<DecompositionWaterfall divergence={residualPair} />)
    expect(screen.getByTestId('waterfall-residual')).toBeInTheDocument()
    expect(screen.getByText(/methodology-driven divergence/)).toBeInTheDocument()
    // It must NOT render a misleading rich chart for a letter-only pair.
    expect(screen.queryByTestId('waterfall-chart')).not.toBeInTheDocument()
  })

  it('renders a consensus state (no chart) for a 0-notch gap', () => {
    render(<DecompositionWaterfall divergence={consensusPair} />)
    expect(screen.getByTestId('waterfall-consensus')).toBeInTheDocument()
    expect(screen.queryByTestId('waterfall-chart')).not.toBeInTheDocument()
  })

  it('renders the labelled bar chart when weighting/input are non-trivial', () => {
    render(<DecompositionWaterfall divergence={richDivergence} />)
    expect(screen.getByTestId('waterfall-chart')).toBeInTheDocument()
    // The accessible legend always lists each bucket + its notch contribution.
    const legend = screen.getByTestId('waterfall-legend')
    expect(legend).toHaveTextContent('Weighting:')
    expect(legend).toHaveTextContent('Input:')
    expect(legend).toHaveTextContent('Methodology:')
  })
})
