// frontend/src/hooks/useReconciliation.ts
// Fallback-REST wiring for a reconciliation sweep + dossier fetch (the streaming AG-UI
// experience lands in package 10). All server state via TanStack Query (arch-10).
import { useMutation, useQuery } from '@tanstack/react-query'
import { apiGet, apiPost } from '@/lib/apiClient'
import type { DossierResponse, ReconciliationRequest } from '@/types/prism'

/** Runs a reconciliation sweep: POST /api/v1/reconciliations. */
export function useReconciliationRun() {
  return useMutation({
    mutationFn: (req: ReconciliationRequest) =>
      apiPost<DossierResponse>('/reconciliations', req),
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
