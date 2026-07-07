// frontend/src/components/prism/DeferredNarrationNote.tsx
// Honest placeholder where LLM narration would appear. The CopilotKit generative cards, the
// AG-UI streaming animation, and the narrator agents that fill `narrative` / `explanation` land
// with the copilot sidecar (package 07). Until then we render NOTHING fabricated (P1) — just this
// note, making clear the deterministic rule/values shown above are authoritative (P2).
import { Sparkles } from 'lucide-react'

interface DeferredNarrationNoteProps {
  /** What the narrator will eventually describe (e.g. "this red flag", "this attribution"). */
  subject?: string
  className?: string
}

export function DeferredNarrationNote({ subject = 'this result', className }: DeferredNarrationNoteProps) {
  return (
    <p
      className={`flex items-start gap-1.5 text-xs text-muted-foreground ${className ?? ''}`}
      data-testid="deferred-narration"
    >
      <Sparkles className="mt-0.5 h-3 w-3 flex-shrink-0 text-muted-foreground" aria-hidden="true" />
      <span>
        Plain-language narration for {subject} streams from the Prism copilot (arriving with the
        agent sidecar). The deterministic rule and values above are authoritative.
      </span>
    </p>
  )
}
