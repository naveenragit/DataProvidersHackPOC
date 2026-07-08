// frontend/src/hooks/useMorningstarContext.ts
// Server-state wiring for the live Morningstar market-context panel (arch-10 — all server state via
// TanStack Query). A read-only GET keyed on the identifier; disabled until a non-blank identifier is
// set. Every provider state (not covered / disabled / re-login / unavailable) comes back as a 200 with
// a `status`, so this query only rejects on a genuine 4xx/5xx (e.g. a malformed identifier → 400).
import { useQuery } from '@tanstack/react-query'
import { apiGet } from '@/lib/apiClient'
import type { MorningstarContextResponse } from '@/types/prism'

/**
 * Fetches live Morningstar analyst research for a real ticker / ISIN / name. Disabled (idle) until
 * `identifier` is a non-blank string. Cached for 5 minutes per identifier; never re-fires on focus.
 */
export function useMorningstarContext(identifier: string | null) {
  const trimmed = identifier?.trim() ?? ''
  return useQuery({
    queryKey: ['morningstar-context', trimmed],
    queryFn: () =>
      apiGet<MorningstarContextResponse>(
        `/market-context/morningstar?identifier=${encodeURIComponent(trimmed)}`,
      ),
    enabled: trimmed.length > 0,
    staleTime: 5 * 60 * 1000,
    retry: 0,
    refetchOnWindowFocus: false,
  })
}
