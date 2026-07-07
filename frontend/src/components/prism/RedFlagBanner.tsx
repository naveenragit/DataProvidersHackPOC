// frontend/src/components/prism/RedFlagBanner.tsx
// The money-moment banner: a prominent destructive callout shown ONLY when a high-severity flag
// fired (e.g. NordStar's STALE_INPUT). Renders the verbatim rule (P2); clicking opens the rule
// modal with the cited, dated evidence rows. Renders nothing when there is no high-severity flag.
import { useState } from 'react'
import { AlertTriangle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { highSeverityFlags } from '@/lib/prismFormat'
import { RuleModal } from './RuleModal'
import type { RedFlagDto } from '@/types/prism'

interface RedFlagBannerProps {
  flags: RedFlagDto[]
}

export function RedFlagBanner({ flags }: RedFlagBannerProps) {
  const [activeFlag, setActiveFlag] = useState<RedFlagDto | null>(null)
  const highFlags = highSeverityFlags(flags)

  if (highFlags.length === 0) return null

  return (
    <div
      className="rounded-lg border border-destructive/50 bg-destructive/10 p-4"
      role="alert"
      data-testid="red-flag-banner"
    >
      <div className="flex items-start gap-3">
        <AlertTriangle className="mt-0.5 h-5 w-5 flex-shrink-0 text-destructive" aria-hidden="true" />
        <div className="min-w-0 flex-1 space-y-2">
          <p className="text-sm font-semibold text-destructive">
            {highFlags.length === 1 ? 'High-severity red flag' : `${highFlags.length} high-severity red flags`}
          </p>
          <ul className="space-y-2">
            {highFlags.map((flag, index) => (
              <li key={`${flag.code}-${index}`} className="space-y-1">
                <p className="font-mono text-xs text-destructive/90">{flag.code}</p>
                <p className="text-sm text-foreground">{flag.rule}</p>
                <Button
                  size="sm"
                  variant="outline"
                  className="border-destructive/40 text-destructive hover:bg-destructive/10"
                  onClick={() => setActiveFlag(flag)}
                >
                  View rule &amp; evidence
                </Button>
              </li>
            ))}
          </ul>
        </div>
      </div>

      <RuleModal
        flag={activeFlag}
        open={activeFlag !== null}
        onOpenChange={(open) => {
          if (!open) setActiveFlag(null)
        }}
      />
    </div>
  )
}
