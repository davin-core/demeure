import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, getHealth } from './api'

afterEach(() => {
  vi.unstubAllGlobals()
  vi.unstubAllEnvs()
})

describe('getHealth', () => {
  it.each(['ok', 'degraded', 'unhealthy'])('validates a %s health response', async (status) => {
    const body = { status, service: 'demeure-api' }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json(body)))

    expect(await getHealth()).toEqual(body)
  })

  it('uses the configured API base URL and forwards cancellation', async () => {
    vi.stubEnv('VITE_API_BASE_URL', '/api/')
    const fetchMock = vi.fn().mockResolvedValue(Response.json({ status: 'ok', service: 'demeure-api' }))
    vi.stubGlobal('fetch', fetchMock)
    const controller = new AbortController()

    await getHealth(controller.signal)

    expect(fetchMock).toHaveBeenCalledWith('/api/health', {
      headers: { accept: 'application/json' }, credentials: 'same-origin', cache: 'no-store',
      signal: expect.any(AbortSignal),
    })
  })

  it('supports an explicitly configured absolute API URL', async () => {
    vi.stubEnv('VITE_API_BASE_URL', 'https://api.example.test/api/')
    const fetchMock = vi.fn().mockResolvedValue(Response.json({ status: 'ok', service: 'demeure-api' }))
    vi.stubGlobal('fetch', fetchMock)

    await getHealth()

    expect(fetchMock).toHaveBeenCalledWith('https://api.example.test/api/health', expect.any(Object))
  })

  it('exposes failed HTTP status codes instead of reporting success', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ status: 'unhealthy' }, { status: 503 })))

    await expect(getHealth()).rejects.toMatchObject({ name: 'ApiError', status: 503 })
  })

  it.each([null, {}, { status: 'ok' }, { status: 'unknown', service: 'api' }, { status: 'ok', service: ' ' }])(
    'rejects an invalid contract: %j', async (body) => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json(body)))

      await expect(getHealth()).rejects.toThrow('unexpected response contract')
    },
  )

  it('reports invalid JSON explicitly', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('not JSON')))

    await expect(getHealth()).rejects.toThrow('invalid JSON')
  })

  it('propagates network failures', async () => {
    const error = new TypeError('Failed to fetch')
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(error))

    await expect(getHealth()).rejects.toBe(error)
  })

  it('preserves cancellation rather than treating it as an HTTP error', async () => {
    const error = new DOMException('Request cancelled', 'AbortError')
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(error))

    await expect(getHealth()).rejects.toBe(error)
  })

  it('records the original error cause', () => {
    const cause = new SyntaxError('invalid JSON')
    const error = new ApiError('Invalid response', 200, { cause })

    expect(error.cause).toBe(cause)
    expect(error.status).toBe(200)
  })
})
