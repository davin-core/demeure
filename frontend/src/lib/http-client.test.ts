import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiUrl, requireJson, sessionExpiredEvent, writeApi } from './http-client'

const guard = (value: unknown): value is { name: string } => typeof value === 'object' && value !== null && 'name' in value && typeof value.name === 'string'
function setup() {
  const dispatchEvent = vi.fn()
  vi.stubGlobal('window', { location: { origin: 'http://localhost:5173' }, dispatchEvent })
  const fetchMock = vi.fn()
  vi.stubGlobal('fetch', fetchMock)
  return { fetchMock, dispatchEvent }
}
afterEach(() => { vi.unstubAllGlobals(); vi.unstubAllEnvs() })

describe('shared transport', () => {
  it.each(['/api', '/api/v1/', 'https://example.test/api/v1'])('never versions health and versions business URLs exactly once: %s', (base) => {
    vi.stubEnv('VITE_API_BASE_URL', base)
    const root = base.replace(/\/$/, '').replace(/\/v1$/, '')
    expect(apiUrl('properties')).toBe(`${root}/v1/properties`)
    expect(apiUrl('health', false)).toBe(`${root}/health`)
  })
  it.each(['', '//example.test/api', '/api?token=secret', '/api#fragment', 'file:///api', 'https://user:password@example.test/api', '/api/v2', 'relative-api'])('rejects malformed configuration %s', (base) => {
    vi.stubEnv('VITE_API_BASE_URL', base)
    expect(() => apiUrl('auth/session')).toThrow()
  })
  it('validates URLs before any cookie-authenticated write, including anonymous login', async () => {
    const { fetchMock } = setup()
    vi.stubEnv('VITE_API_BASE_URL', 'https://other.test/api')
    await expect(writeApi('auth/login', {}, 204, undefined, { authenticated: false })).rejects.toThrow('same-origin')
    expect(fetchMock).not.toHaveBeenCalled()
  })
  it('preserves Problem Details field errors, request ID and Retry-After', async () => {
    setup().fetchMock.mockResolvedValue(Response.json({ title: 'Validation failed', traceId: 'test-trace', errors: { Email: ['Invalid email'] } },
      { status: 400, headers: { 'Content-Type': 'application/problem+json', 'Retry-After': '30' } }))
    await expect(requireJson('properties', guard)).rejects.toMatchObject({
      message: 'Validation failed', status: 400, traceId: 'test-trace', errors: { Email: ['Invalid email'] }, retryAfter: '30',
    })
  })
  it('does not render server internals from unexpected failures', async () => {
    setup().fetchMock.mockResolvedValue(Response.json({ title: 'secret connection string', detail: 'database details', traceId: 'trace' },
      { status: 500, headers: { 'Content-Type': 'application/problem+json' } }))
    await expect(requireJson('properties', guard)).rejects.toMatchObject({ message: 'API request failed (HTTP 500).', traceId: 'trace' })
  })
  it.each([null, { errors: [] }, { errors: { Email: 'invalid' } }, { errors: { Email: [42] } }])('rejects malformed Problem Details: %j', async (body) => {
    setup().fetchMock.mockResolvedValue(Response.json(body, { status: 400, headers: { 'Content-Type': 'application/problem+json' } }))
    await expect(requireJson('properties', guard)).rejects.toThrow('invalid')
  })
  it('notifies session expiration only for authenticated API failures', async () => {
    const { fetchMock, dispatchEvent } = setup()
    fetchMock.mockResolvedValue(new Response(null, { status: 401 }))
    await expect(requireJson('properties', guard)).rejects.toMatchObject({ status: 401 })
    expect(dispatchEvent).toHaveBeenCalledWith(expect.objectContaining({ type: sessionExpiredEvent }))
    dispatchEvent.mockClear()
    await expect(requireJson('auth/login', guard, { authenticated: false })).rejects.toMatchObject({ status: 401 })
    expect(dispatchEvent).not.toHaveBeenCalled()
  })
  it('combines a timeout with caller cancellation and forwards organization scope', async () => {
    const { fetchMock } = setup()
    fetchMock.mockResolvedValue(Response.json({ name: 'valid' }))
    const controller = new AbortController()
    await requireJson('properties', guard, { organizationId: 'org-id', signal: controller.signal })
    const options: RequestInit = fetchMock.mock.calls[0][1]
    expect(options.headers).toMatchObject({ 'x-organization-id': 'org-id' })
    expect(options.signal?.aborted).toBe(false)
    controller.abort()
    expect(options.signal?.aborted).toBe(true)
  })
  it('fetches a fresh CSRF token for every write and validates creation DTOs', async () => {
    const { fetchMock } = setup()
    fetchMock.mockResolvedValueOnce(Response.json({ token: 'first' })).mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(Response.json({ token: 'second' })).mockResolvedValueOnce(Response.json({ name: 'created' }, { status: 201 }))
    await writeApi('auth/logout', undefined, 204)
    expect(await writeApi('auth/register', { name: 'created' }, 201, guard)).toEqual({ name: 'created' })
    expect(fetchMock.mock.calls[1][1].headers).toMatchObject({ 'x-csrf-token': 'first' })
    expect(fetchMock.mock.calls[3][1].headers).toMatchObject({ 'x-csrf-token': 'second', 'content-type': 'application/json' })
    expect(fetchMock.mock.calls[3][1].body).toBe('{"name":"created"}')
  })
  it('never retries or redirects a failed write', async () => {
    const { fetchMock } = setup()
    fetchMock.mockResolvedValueOnce(Response.json({ token: 'token' })).mockResolvedValueOnce(new Response(null, { status: 429 }))
    await expect(writeApi('auth/register', {}, 201, guard)).rejects.toMatchObject({ status: 429 })
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })
  it('rejects success-shaped but wrong HTTP read status', async () => {
    setup().fetchMock.mockResolvedValue(Response.json({ name: 'valid' }, { status: 201 }))
    await expect(requireJson('properties', guard)).rejects.toMatchObject({ status: 201, message: 'The API returned an unexpected success status.' })
  })
})
