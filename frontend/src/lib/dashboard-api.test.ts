import { afterEach, describe, expect, it, vi } from 'vitest'
import { defaultDashboardQuery, getDashboard, isDashboard, validateDashboardQuery, type DashboardResponse } from './dashboard-api'
import { loginDestination } from './login-navigation'

const organizationId = '22222222-2222-4222-8222-222222222222'
const id = '11111111-1111-4111-8111-111111111111'
const query = { startDate: '2026-02-01', endDate: '2026-02-28' }
export const emptyDashboard: DashboardResponse = {
  organizationId, ...query, propertyId: null, generatedAt: '2026-02-28T12:00:00Z',
  properties: [], totalProperties: 0, totalUnits: 0, occupiedUnits: 0, vacantUnits: 0, unavailableUnits: 0,
  occupancyRate: null, amounts: [], pendingMaintenanceRequests: 0, expiringLeaseCount: 0, expiringLeases: [],
  months: [{ month: '2026-02-01', amounts: [] }], recentActivities: [],
}
afterEach(() => { vi.unstubAllGlobals(); vi.unstubAllEnvs() })

describe('dashboard query and contracts', () => {
  it('defaults to the current UTC month including leap days', () => {
    expect(defaultDashboardQuery(new Date('2024-02-29T23:59:59Z'))).toEqual({ startDate: '2024-02-01', endDate: '2024-02-29' })
  })
  it.each([
    { ...query, startDate: '' }, { ...query, endDate: '2026-02-30' }, { ...query, endDate: '2026-01-31' },
    { ...query, startDate: '2019-01-01' }, { ...query, propertyId: 'bad' }, { ...query, endDate: '9999-12-31' },
  ])('rejects invalid filter %j', (filter) => { expect(() => validateDashboardQuery(filter)).toThrow() })
  it('allows exactly five inclusive calendar years and a valid property', () => {
    expect(() => validateDashboardQuery({ startDate: '2024-01-01', endDate: '2028-12-31', propertyId: id })).not.toThrow()
    expect(() => validateDashboardQuery({ startDate: '2024-01-01', endDate: '2029-01-01' })).toThrow()
  })
  it('matches calendar-year arithmetic at a leap-day start', () => {
    expect(() => validateDashboardQuery({ startDate: '2024-02-29', endDate: '2029-02-27' })).not.toThrow()
    expect(() => validateDashboardQuery({ startDate: '2024-02-29', endDate: '2029-02-28' })).toThrow()
  })
  it('accepts a genuine empty response without manufacturing a rate or currency', () => {
    expect(isDashboard(emptyDashboard, organizationId, query)).toBe(true)
  })
  it.each([
    { organizationId: id }, { endDate: '2026-02-27' }, { propertyId: id }, { totalUnits: -1 },
    { totalUnits: 1 }, { occupancyRate: 0 }, { amounts: [{ currency: 'CAD', rentalIncome: Infinity, outstandingRent: 0, expenses: 0 }] },
    { months: [] }, { months: [{ month: '2026-03-01', amounts: [] }] }, { generatedAt: 'invalid' },
    { expiringLeases: [{ id, propertyId: id, propertyName: 'Test', unitNumber: '1', endDate: '2026-02-30' }] },
    { recentActivities: [{ id, kind: 'invented', label: 'Test', status: 'Open', occurredAt: '2026-02-01T00:00:00Z' }] },
  ])('rejects invalid or mismatched response %j', (changes) => {
    expect(isDashboard({ ...emptyDashboard, ...changes }, organizationId, query)).toBe(false)
  })
  it('checks non-finite occupancy and duplicate currencies', () => {
    expect(isDashboard({ ...emptyDashboard, totalUnits: 1, occupiedUnits: 1, occupancyRate: NaN }, organizationId, query)).toBe(false)
    const money = { currency: 'CAD', rentalIncome: 100, outstandingRent: 0, expenses: 50 }
    expect(isDashboard({ ...emptyDashboard, amounts: [money, money] }, organizationId, query)).toBe(false)
  })
  it('sends explicit organization and filters through the shared transport', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ...emptyDashboard, propertyId: id }), { headers: { 'Content-Type': 'application/json' } }))
    vi.stubGlobal('fetch', fetch)
    await getDashboard(organizationId, { ...query, propertyId: id })
    expect(fetch.mock.calls[0][0]).toBe(`/api/v1/dashboard?startDate=2026-02-01&endDate=2026-02-28&propertyId=${id}`)
    expect(new Headers(fetch.mock.calls[0][1].headers).get('X-Organization-Id')).toBe(organizationId)
    expect(fetch.mock.calls[0][1].credentials).toBe('same-origin')
  })
  it('rejects invalid scope before making a request', () => {
    const fetch = vi.fn()
    vi.stubGlobal('fetch', fetch)
    expect(() => getDashboard('bad', query)).toThrow()
    expect(fetch).not.toHaveBeenCalled()
  })
  it('returns owners to the canonical dashboard and preserves the legacy landlord alias', () => {
    const session = { userId: id, email: 'owner@example.test', displayName: 'Owner', globalRoles: [],
      memberships: [{ organizationId, organizationName: 'Test', role: 'OrganizationOwner' }] }
    expect(loginDestination(session, null)).toBe('/app/dashboard')
    expect(loginDestination(session, '/landlord')).toBe('/app/dashboard')
    expect(loginDestination({ ...session, memberships: [] }, '/app/dashboard')).toBe('/public')
  })
})
