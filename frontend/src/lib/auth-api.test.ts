import { afterEach, describe, expect, it, vi } from 'vitest'
import { confirmEmail, fieldError, login, logoutAll, registerOwner, requestRecoveryEmail, resetPassword, validateLogin, validateRegistration, validateResetPassword } from './auth-api'
import { loginDestination } from './login-navigation'
import type { Session } from './session-api'

const session: Session = { userId: '11111111-1111-4111-8111-111111111111', email: 'test@example.test', displayName: 'Test', globalRoles: [],
  memberships: [{ organizationId: '22222222-2222-4222-8222-222222222222', organizationName: 'Test', role: 'Tenant' }] }
const registration = { email: session.email, password: 'StrongTestPass!123', displayName: 'Test', organizationName: 'Test', organizationSlug: 'test-organization' }
function setup() {
  vi.stubGlobal('window', { location: { origin: 'http://localhost:5173' }, dispatchEvent: vi.fn() })
  const fetchMock = vi.fn()
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}
afterEach(() => { vi.unstubAllGlobals(); vi.unstubAllEnvs() })
describe('authentication validation', () => {
  it('validates login without imposing new password rules on existing credentials', () => {
    expect(validateLogin({ email: session.email, password: 'old-password' })).toEqual({})
    expect(validateLogin({ email: 'invalid', password: '' })).toHaveProperty('email')
    expect(validateLogin({ email: session.email, password: 'x'.repeat(129) })).toHaveProperty('password')
  })
  it('matches registration and reset-password guidance without treating it as authoritative', () => {
    expect(validateRegistration(registration)).toEqual({})
    for (const password of ['short', 'lowercasepassword123!', 'UPPERCASEPASSWORD123!', 'NoDigitPassword!', 'NoSymbolPassword123', 'Aa1!Aa1!Aa1!']) {
      expect(validateRegistration({ ...registration, password })).toHaveProperty('password')
    }
    expect(validateRegistration({ ...registration, organizationSlug: 'Invalid--slug' })).toHaveProperty('organizationSlug')
    expect(validateRegistration({ ...registration, displayName: ' ' })).toHaveProperty('displayName')
    expect(validateResetPassword({ email: session.email, newPassword: registration.password, token: 'test' })).toEqual({})
    expect(validateResetPassword({ email: session.email, newPassword: 'short', token: '' })).toHaveProperty('newPassword')
  })
  it('maps PascalCase server validation fields to camelCase controls', () => {
    expect(fieldError({ Email: ['Invalid'], email: ['Required'] }, 'email')).toBe('Invalid Required')
    expect(fieldError({}, 'email')).toBeUndefined()
  })
  it.each(['https://evil.test', '//evil.test', '/tenant?next=evil', '/administration', '/tenant/leases', null])('allows returnTo only for exact authorized layout routes: %s', (returnTo) => {
    expect(loginDestination(session, returnTo)).toBe('/tenant')
  })
  it('returns a platform-only user only to explicit administration and members to public', () => {
    expect(loginDestination({ ...session, globalRoles: ['SuperAdministrator'], memberships: [] }, '/landlord')).toBe('/administration')
    expect(loginDestination({ ...session, memberships: [] }, null)).toBe('/public')
  })
})
describe('authentication clients', () => {
  it('verifies the real session after successful login before returning permissions', async () => {
    const fetchMock = setup().mockResolvedValueOnce(Response.json({ token: 'token' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 })).mockResolvedValueOnce(Response.json(session))
    expect(await login({ email: session.email, password: registration.password })).toEqual(session)
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual(['/api/v1/auth/csrf', '/api/v1/auth/login', '/api/v1/auth/session'])
  })
  it('never fabricates a session after login or assumes registration signs in', async () => {
    setup().mockResolvedValueOnce(Response.json({ token: 'token' })).mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
    await expect(login({ email: session.email, password: registration.password })).rejects.toMatchObject({ status: 401 })
    const fetchMock = setup().mockResolvedValueOnce(Response.json({ token: 'token' })).mockResolvedValueOnce(Response.json({
      userId: session.userId, organizationId: session.memberships[0].organizationId,
    }, { status: 201 }))
    expect(await registerOwner(registration)).toHaveProperty('userId', session.userId)
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })
  it('rejects invalid fields before network requests', async () => {
    const fetchMock = setup()
    await expect(login({ email: 'invalid', password: '' })).rejects.toHaveProperty('errors.email')
    await expect(confirmEmail({ userId: 'invalid', token: '' })).rejects.toHaveProperty('errors.userId')
    await expect(resetPassword({ email: session.email, token: '', newPassword: 'bad' })).rejects.toHaveProperty('errors.newPassword')
    expect(fetchMock).not.toHaveBeenCalled()
  })
  it.each(['verification-email', 'forgot-password'] as const)('supports %s without exposing account existence', async (kind) => {
    const fetchMock = setup().mockResolvedValueOnce(Response.json({ token: 'token' })).mockResolvedValueOnce(new Response(null, { status: 202 }))
    await requestRecoveryEmail(kind, session.email)
    expect(fetchMock.mock.calls[1][0]).toBe(`/api/v1/auth/${kind}`)
  })
  it('supports logout-all, confirmation and reset using the same transport', async () => {
    const fetchMock = setup().mockImplementation((url: string) => Promise.resolve(url.endsWith('/csrf') ? Response.json({ token: 'fresh' }) : new Response(null, { status: 204 })))
    await logoutAll()
    await confirmEmail({ userId: session.userId, token: 'verification-token' })
    await resetPassword({ email: session.email, token: 'reset-token', newPassword: registration.password })
    expect(fetchMock.mock.calls.map(([url]) => url)).toContain('/api/v1/auth/logout-all')
    expect(fetchMock).toHaveBeenCalledTimes(6)
  })
})
