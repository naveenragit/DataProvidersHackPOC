// frontend/src/hooks/useReconciliation.ts
// Server-state wiring for a reconciliation sweep + dossier fetch (arch-10 — all server state via
// TanStack Query). The sweep is modelled as a QUERY keyed on (issuer, as-of): it auto-runs when an
// issuer is selected, is cached per issuer/as-of, and is StrictMode-safe. A mutation fired from an
// effect is NOT: React 18's double-mount tears down the observer that started it, stranding
// `isPending` forever even though the POST succeeds.
import { useQuery } from '@tanstack/react-query'
import { apiGet, apiPost } from '@/lib/apiClient'
import type { DossierResponse } from '@/types/prism'

/**
 * Runs a reconciliation sweep (POST /api/v1/reconciliations) for the given issuer + as-of date.
 * Auto-runs once an issuer is set; `refetch()` re-runs it. Idle (disabled) when `issuerId` is null.
 */
export function useReconciliationRun(issuerId: string | null, asOf: string) {
  return useQuery({
    queryKey: ['reconciliation-run', issuerId, asOf],
    queryFn: () => apiPost<DossierResponse>('/reconciliations', { issuerId, asOf }),
    enabled: !!issuerId,
    staleTime: Infinity,
    retry: 0,
    refetchOnWindowFocus: false,
    // The "run" is a non-idempotent POST, so never let a reconnect silently re-fire it (a fresh
    // Cosmos write + ~10 gpt-5.4 calls). staleTime:Infinity already neutralises this; pin it
    // explicitly so a future default change can't regress it (adversary STK-10-03 / ARC-10-01).
    refetchOnReconnect: false,
  })
}

/** Fetches a persisted dossier by id: GET /api/v1/reconciliations/{id}. Disabled until `id` is set. */
export function useDossier(id?: string) {
  return useQuery({
    queryKey: ['reconciliation', id],
    queryFn: () => apiGet<DossierResponse>(`/reconciliations/${id}`),
    enabled: !!id,
  })
}
