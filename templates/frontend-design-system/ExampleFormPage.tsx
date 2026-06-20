// Example of a properly styled page using the design system.
// Compare against an UNSTYLED page: if inputs look like plain browser boxes and the
// background is white with serif fonts, Tailwind is NOT wired up — fix the config first.
import { useState } from 'react'

export default function ExampleFormPage() {
  const [submitting, setSubmitting] = useState(false)

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-100">New Loan Application</h1>
        <p className="text-gray-500 mt-1 text-sm">
          Submit borrower details to run the credit risk pipeline.
        </p>
      </div>

      <div className="card max-w-2xl space-y-4">
        <div>
          <label className="section-title block">Company name</label>
          <input className="input" placeholder="Acme Manufacturing Inc." />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="section-title block">Loan amount</label>
            <input className="input" placeholder="$2,500,000" />
          </div>
          <div>
            <label className="section-title block">Loan purpose</label>
            <input className="input" placeholder="Working capital" />
          </div>
        </div>

        <div>
          <label className="section-title block">Financial statements</label>
          <textarea className="input min-h-[140px] resize-y" placeholder="Paste financials..." />
        </div>

        <div className="flex items-center gap-3 pt-2">
          <button
            className="btn-primary"
            disabled={submitting}
            onClick={() => setSubmitting(true)}
          >
            {submitting ? 'Submitting...' : 'Submit for assessment'}
          </button>
          <button className="btn-secondary">Cancel</button>
          <span className="badge-info ml-auto">Live Azure</span>
        </div>
      </div>
    </div>
  )
}
