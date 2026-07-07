// frontend/src/components/prism/ConfidenceMeter.tsx
// Renders the deterministic 0..1 confidence score from the dossier as a labelled bar. Pure
// presentation of an API-provided value (P2) — it computes nothing.
import { formatConfidence } from '@/lib/prismFormat'

interface ConfidenceMeterProps {
  confidence: number
}

export function ConfidenceMeter({ confidence }: ConfidenceMeterProps) {
  const pct = Math.round(Math.min(1, Math.max(0, confidence)) * 100)
  return (
    <div className="min-w-[9rem]" data-testid="confidence-meter">
      <div className="mb-1 flex items-center justify-between gap-3">
        <span className="text-xs uppercase tracking-wide text-muted-foreground">Confidence</span>
        <span className="font-mono text-xs text-foreground">{formatConfidence(confidence)}</span>
      </div>
      <div
        className="h-1.5 w-full overflow-hidden rounded-full bg-muted"
        role="progressbar"
        aria-valuenow={pct}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label="Reconciliation confidence"
      >
        <div className="h-full rounded-full bg-primary transition-all" style={{ width: `${pct}%` }} />
      </div>
    </div>
  )
}
