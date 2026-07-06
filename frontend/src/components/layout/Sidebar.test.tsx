import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { Sidebar } from './Sidebar'

describe('Sidebar', () => {
  function renderSidebar() {
    return render(
      <MemoryRouter>
        <Sidebar />
      </MemoryRouter>,
    )
  }

  it('renders the three Prism nav groups', () => {
    renderSidebar()
    // Group labels (Prism also appears in the brand block, so assert presence, not uniqueness).
    for (const label of ['Prism', 'Architecture', 'Settings']) {
      expect(screen.getAllByText(label).length).toBeGreaterThan(0)
    }
  })

  it('renders the five nav items as links', () => {
    renderSidebar()
    for (const item of ['Issuers', 'Reconciliation', 'Workflow', 'Architecture', 'Settings']) {
      expect(screen.getByRole('link', { name: item })).toBeInTheDocument()
    }
  })
})
