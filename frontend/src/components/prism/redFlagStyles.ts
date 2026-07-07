// frontend/src/components/prism/redFlagStyles.ts
// Shared severity → styling maps for red-flag surfaces (banner, panel, modal). Keeps the
// destructive (red, arch-10) treatment for high-severity flags and an amber caution tint for
// medium, consistent across every component.
import type { BadgeProps } from '@/components/ui/badge'
import type { Severity } from '@/types/prism'

export const SEVERITY_LABEL: Record<Severity, string> = {
  high: 'High',
  medium: 'Medium',
  low: 'Low',
}

/** Base shadcn Badge variant per severity (high = destructive/red per arch-10). */
export const SEVERITY_BADGE_VARIANT: Record<Severity, BadgeProps['variant']> = {
  high: 'destructive',
  medium: 'secondary',
  low: 'outline',
}

/** Extra badge classes: amber caution tint for medium; none for high/low. */
export const SEVERITY_BADGE_CLASS: Record<Severity, string> = {
  high: '',
  medium: 'border-transparent bg-amber-500/15 text-amber-500',
  low: '',
}
