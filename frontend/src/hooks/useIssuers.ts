// frontend/src/hooks/useIssuers.ts
// TanStack Query hook for the issuer cast (arch-10: all server state via TanStack Query).
import { useQuery } from '@tanstack/react-query'
import { apiGet } from '@/lib/apiClient'
import type { IssuerListItem } from '@/types/prism'

/** Lists the issuer cast from the real API: GET /api/v1/issuers. */
export function useIssuers() {
  return useQuery({
    queryKey: ['issuers'],
    queryFn: () => apiGet<IssuerListItem[]>('/issuers'),
  })
}
