// frontend/src/components/prism/ScopeNotice.tsx
// Amber scope banner (P4/P5). Prism reconciles provider data; it makes no investment decisions.
// The interactive confirm-scope human gate (CopilotKit `renderAndWaitForResponse`) lands with the
// agent sidecar (package 07) — until then this static notice keeps the scope explicit and honest.
import { ShieldQuestion } from 'lucide-react'

export function ScopeNotice() {
  return (
    <div
      className="flex items-start gap-3 rounded-lg border border-amber-500/40 bg-amber-500/10 p-3"
      role="note"
      data-testid="scope-notice"
    >
      <ShieldQuestion className="mt-0.5 h-4 w-4 flex-shrink-0 text-amber-500" aria-hidden="true" />
      <p className="text-sm text-foreground">
        <span className="font-medium text-amber-500">Scope: reconciliation only.</span>{' '}
        Prism explains why provider ratings diverge and cites the evidence. It does not make
        investment decisions. The interactive confirm-scope gate arrives with the Prism copilot.
      </p>
    </div>
  )
}
