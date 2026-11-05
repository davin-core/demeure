import { afterEach, describe, expect, it, vi } from 'vitest'
import { getSession, isSession, logoutSession, type Session } from './session-api'

const userId = '11111111-1111-4111-8111-111111111111'
const organizationId = '22222222-2222-4222-8222-222222222222'
const session: Session = {
  userId, email: 'test@example.test', displayName: 'Test Account', globalRoles: [],
  memberships: [{ organizationId, organizationName: 'Test organization', role: 'OrganizationOwner' }],
}

afterEach(() => { vi.unstubAllGlobals(); vi.unstubAllEnvs() })
function setup() {
  vi.stubGlobal('window', { location: { origin: 'http://localhost:5173' }, dispatchEvent: vi.fn() })
  const fetchMock = vi.fn()
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('session contract', () => {
  it('accepts the actual session DTO, including ordinary members without a portal role', () => {
    expect(isSession(session)).toBe(true)
    expect(isSession({ ...session, memberships: [{ ...session.memberships[0], role: '' }] })).toBe(true)
    expect(isSession({ ...session, memberships: [], globalRoles: ['SuperAdministrator'] })).toBe(true)
  })
  it.each([
    null, {}, { ...session, userId: 'invalid' }, { ...session, email: '' },
    { ...session, displayName: ' ' }, { ...session, globalRoles: [42] },
    { ...session, memberships: [{}] },
    { ...session, memberships: [{ ...session.memberships[0], organizationId: '00000000-0000-0000-0000-000000000000' }] },
    { ...session, memberships: [session.memberships[0], session.memberships[0]] },
  ])('rejects invalid or ambiguous sessions: %j', (value) => { expect(isSession(value)).toBe(false) })
})

describe('session requests', () => {
  it('uses canonical v1, cookies, no-store and caller cancellation', async () => {
    const fetchMock = setup().mockResolvedValue(Response.json(session))
    const controller = new AbortController()
    expect(await getSession(controller.signal)).toEqual(session)
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/auth/session', {
      credentials: 'same-origin', cache: 'no-store', headers: { accept: 'application/json' }, signal: expect.any(AbortSignal),
    })
  })
  it.each(['/api/v1/', 'http://localhost:5173/api/v1/'])('supports an already versioned base %s', async (base) => {
    const fetchMock = setup().mockResolvedValue(Response.json(session))
    vi.stubEnv('VITE_API_BASE_URL', base)
    await getSession()
    expect(fetchMock).toHaveBeenCalledWith(`${base.startsWith('http') ? 'http://localhost:5173' : ''}/api/v1/auth/session`, expect.any(Object))
  })
  it('rejects cross-origin cookie configuration before making a request', async () => {
    const fetchMock = setup()
    vi.stubEnv('VITE_API_BASE_URL', 'https://api.example.test/api')
    await expect(getSession()).rejects.toThrow('same-origin API')
    expect(fetchMock).not.toHaveBeenCalled()
  })
  it('treats only 401 as anonymous, never a network or server failure', async () => {
    setup().mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(null, { status: 403 }))
      .mockResolvedValueOnce(new Response(null, { status: 503 }))
    expect(await getSession()).toBeNull()
    await expect(getSession()).rejects.toMatchObject({ status: 403 })
    await expect(getSession()).rejects.toMatchObject({ status: 503 })
  })
  it('reports malformed JSON and session shape explicitly', async () => {
    setup().mockResolvedValueOnce(new Response('invalid')).mockResolvedValueOnce(Response.json({}))
    await expect(getSession()).rejects.toThrow('invalid JSON')
    await expect(getSession()).rejects.toThrow('unexpected response contract')
  })
  it('propagates network and cancellation errors', async () => {
    const error = new DOMException('Aborted', 'AbortError')
    setup().mockRejectedValue(error)
    await expect(getSession()).rejects.toBe(error)
  })
})

describe('logout', () => {
  it('gets a fresh CSRF token for every write and only accepts 204', async () => {
    const fetchMock = setup().mockResolvedValueOnce(Response.json({ token: 'fresh-token' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    const controller = new AbortController()
    await logoutSession(controller.signal)
    expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/v1/auth/csrf', {
      credentials: 'same-origin', cache: 'no-store', signal: expect.any(AbortSignal), headers: { accept: 'application/json' },
    })
    expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/v1/auth/logout', {
      credentials: 'same-origin', cache: 'no-store', signal: expect.any(AbortSignal), method: 'POST',
      headers: { accept: 'application/json', 'x-csrf-token': 'fresh-token' },
    })
  })
  it.each([{}, { token: '' }, { token: 3 }])('does not sign out with an invalid CSRF response %j', async (body) => {
    const fetchMock = setup().mockResolvedValue(Response.json(body))
    await expect(logoutSession()).rejects.toThrow('unexpected response contract')
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
  it('surfaces CSRF bootstrap failure without issuing the write', async () => {
    const fetchMock = setup().mockResolvedValue(new Response(null, { status: 500 }))
    await expect(logoutSession()).rejects.toMatchObject({ status: 500 })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
  it.each([200, 400, 401, 500])('never reports failed or unexpected logout status %s as success', async (status) => {
    setup().mockResolvedValueOnce(Response.json({ token: 'fresh-token' })).mockResolvedValueOnce(new Response(null, { status }))
    await expect(logoutSession()).rejects.toMatchObject({ status })
  })
})
