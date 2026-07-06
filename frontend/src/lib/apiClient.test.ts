import { afterEach, describe, it, expect, vi } from 'vitest'
import { ApiError, apiGet, apiPost } from './apiClient'

/** Minimal Response-like stub so the test does not depend on the global Response constructor. */
function stubFetch(status: number, body: unknown) {
  vi.stubGlobal(
    'fetch',
    vi.fn(
      async () =>
        ({
          ok: status >= 200 && status < 300,
          status,
          statusText: `HTTP ${status}`,
          json: async () => body,
        }) as unknown as Response,
    ),
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('apiClient', () => {
  it('returns the typed JSON body on a 2xx response', async () => {
    const payload = [{ issuerId: 'iss_1', legalName: 'Acme Corp' }]
    stubFetch(200, payload)

    const result = await apiGet<typeof payload>('/issuers')

    expect(result).toEqual(payload)
  })

  it('throws ApiError parsing the standard error envelope on a non-2xx response', async () => {
    stubFetch(404, { error: { code: 'NOT_FOUND', message: 'no such issuer' } })

    let thrown: unknown
    try {
      await apiGet('/issuers/nope')
    } catch (e) {
      thrown = e
    }

    expect(thrown).toBeInstanceOf(ApiError)
    const err = thrown as ApiError
    expect(err.code).toBe('NOT_FOUND')
    expect(err.status).toBe(404)
    expect(err.message).toBe('no such issuer')
  })

  it('falls back to code UNKNOWN when the error body is not JSON', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          ({
            ok: false,
            status: 500,
            statusText: 'HTTP 500',
            json: async () => {
              throw new SyntaxError('Unexpected token < in JSON')
            },
          }) as unknown as Response,
      ),
    )

    let thrown: unknown
    try {
      await apiGet('/issuers')
    } catch (e) {
      thrown = e
    }

    expect(thrown).toBeInstanceOf(ApiError)
    const err = thrown as ApiError
    expect(err.code).toBe('UNKNOWN')
    expect(err.status).toBe(500)
  })

  it('returns undefined for a 204 No Content response without parsing the body', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          ({
            ok: true,
            status: 204,
            statusText: 'HTTP 204',
            json: async () => {
              throw new Error('json() must not be called on a 204')
            },
          }) as unknown as Response,
      ),
    )

    const result = await apiGet<undefined>('/issuers/iss_1/ack')

    expect(result).toBeUndefined()
  })

  it('apiPost issues a POST with the JSON body and returns the typed response', async () => {
    const responseBody = { dossierId: 'dos_1' }
    const fetchMock = vi.fn(
      async () =>
        ({
          ok: true,
          status: 200,
          statusText: 'HTTP 200',
          json: async () => responseBody,
        }) as unknown as Response,
    )
    vi.stubGlobal('fetch', fetchMock)

    const result = await apiPost<typeof responseBody>('/reconciliations', { issuerId: 'iss_1' })

    expect(result).toEqual(responseBody)
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/reconciliations',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ issuerId: 'iss_1' }),
      }),
    )
  })
})
