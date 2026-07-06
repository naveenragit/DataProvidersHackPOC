// frontend/src/lib/apiClient.ts
//
// Typed fetch wrapper over the Prism REST API (base `/api/v1`, proxied to the C# backend
// on :8000 by Vite). Fail-loud (P1 / arch-03): on any non-2xx it parses the standard
// `{ error: { code, message, details } }` envelope and THROWS `ApiError` — it never
// returns fabricated placeholder data.
import type { ApiErrorBody } from '@/types/prism'

const API_BASE = '/api/v1'

/**
 * Auth-token injection seam. Default = no auth (matches the demo's AllowAnonymous reads), so
 * no bearer token is ever hardcoded in the client bundle. Package 07/08 wires the real MSAL
 * access-token accessor here via `setAuthTokenProvider`, and `request()` adds
 * `Authorization: Bearer <token>` only when a provider is registered and returns a token.
 */
type AuthTokenProvider = () => string | null

let authTokenProvider: AuthTokenProvider | null = null

/** Registers the access-token accessor (pkg 07/08). Pass `null` to clear it (e.g. on sign-out). */
export function setAuthTokenProvider(provider: AuthTokenProvider | null): void {
  authTokenProvider = provider
}

/** Thrown for every non-2xx response. Carries the server error code + HTTP status. */
export class ApiError extends Error {
  readonly code: string
  readonly status: number
  readonly details?: unknown

  constructor(code: string, message: string, status: number, details?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.code = code
    this.status = status
    this.details = details
  }
}

function isApiErrorBody(value: unknown): value is ApiErrorBody {
  return (
    typeof value === 'object' &&
    value !== null &&
    'error' in value &&
    typeof (value as { error: unknown }).error === 'object' &&
    (value as { error: unknown }).error !== null
  )
}

async function toApiError(response: Response): Promise<ApiError> {
  let code = 'UNKNOWN'
  let message = response.statusText || `Request failed with status ${response.status}`
  let details: unknown

  try {
    const body: unknown = await response.json()
    if (isApiErrorBody(body)) {
      code = body.error.code ?? code
      message = body.error.message ?? message
      details = body.error.details
    }
  } catch {
    // Body was not JSON (or empty) — keep the status-derived fallback (still fail-loud).
  }

  return new ApiError(code, message, response.status, details)
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  // Build headers from the caller's RequestInit FIRST, then apply defaults LAST, so a caller
  // supplying its own `headers` can never drop the JSON Content-Type. `...init` is spread
  // before `headers` in the fetch options for the same reason.
  const headers = new Headers(init?.headers)
  if (!headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  // Auth seam: attach a bearer token only when a provider is registered and returns one.
  const token = authTokenProvider?.() ?? null
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers,
  })

  if (!response.ok) {
    throw await toApiError(response)
  }

  // 204 No Content → nothing to parse.
  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

/** GET `/api/v1{path}` → typed body; throws `ApiError` on non-2xx. */
export function apiGet<T>(path: string): Promise<T> {
  return request<T>(path, { method: 'GET' })
}

/** POST `/api/v1{path}` with a JSON body → typed body; throws `ApiError` on non-2xx. */
export function apiPost<T>(path: string, body: unknown): Promise<T> {
  return request<T>(path, { method: 'POST', body: JSON.stringify(body) })
}
