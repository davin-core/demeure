import { afterEach, describe, expect, it, vi } from 'vitest'
import { collectionParameters, isPage, isResource, listResources, readResource, type ResourceKind } from './resource-api'

const id = '11111111-1111-4111-8111-111111111111'
const organizationId = '22222222-2222-4222-8222-222222222222'
const otherOrganization = '33333333-3333-4333-8333-333333333333'
const property = { id, organizationId, resourceType: 'property', name: 'Test property', city: 'Test city', status: 'Active' }
const guard = (value: unknown): value is typeof property => isResource('properties', organizationId, value)
afterEach(() => { vi.unstubAllGlobals(); vi.unstubAllEnvs() })

describe('pagination query contracts', () => {
  it('matches defaults and encodes allowed sorting/filtering once', () => {
    expect(collectionParameters('properties').toString()).toBe('page=1&pageSize=20')
    expect(collectionParameters('properties', { page: 2, pageSize: 100, status: 'Active', sortBy: 'createdAt', sortDirection: 'desc' }).toString()).toBe('page=2&pageSize=100&status=Active&sortBy=createdAt&sortDirection=desc')
  })
  it.each([{ page: 0 }, { page: -1 }, { page: 1.5 }, { pageSize: 0 }, { pageSize: 101 }, { page: Number.MAX_SAFE_INTEGER }, { status: 'active' }, { status: '' }])('rejects invalid query %j', (query) => {
    expect(() => collectionParameters('properties', query)).toThrow()
  })
  it('validates the Int32 offset ceiling without silently clamping', () => {
    expect(() => collectionParameters('properties', { page: 2147483648, pageSize: 1 })).not.toThrow()
    expect(() => collectionParameters('properties', { page: 2147483649, pageSize: 1 })).toThrow()
  })
  it('accepts exact status names for each resource', () => {
    for (const [kind, status] of [['units', 'Occupied'], ['tenants', 'Archived'], ['rental-applications', 'UnderReview'], ['leases', 'Terminated'], ['payments', 'Refunded'], ['maintenance-requests', 'InProgress']] as const) {
      expect(collectionParameters(kind, { status }).get('status')).toBe(status)
    }
  })
})
describe('response contracts', () => {
  it('rejects foreign organization and mismatched discriminators', () => {
    expect(guard(property)).toBe(true)
    expect(guard({ ...property, organizationId: otherOrganization })).toBe(false)
    expect(guard({ ...property, resourceType: 'tenant' })).toBe(false)
    expect(guard({ ...property, status: 'Unknown' })).toBe(false)
    expect(guard({ ...property, name: null })).toBe(false)
  })
  it.each([
    ['units', { resourceType: 'unit', propertyId: id, number: '1', status: 'Available' }],
    ['tenants', { resourceType: 'tenant', firstName: 'Test', lastName: 'Tenant', status: 'Active' }],
    ['rental-applications', { resourceType: 'rentalApplication', tenantId: id, unitId: id, status: 'Draft' }],
    ['leases', { resourceType: 'lease', tenantId: id, unitId: id, startDate: '2026-01-01', endDate: '2026-12-31', monthlyRent: 1200.50, currency: 'CAD', status: 'Draft' }],
    ['payments', { resourceType: 'payment', leaseId: id, amount: 100, currency: 'CAD', status: 'Pending' }],
    ['maintenance-requests', { resourceType: 'maintenance', propertyId: id, tenantId: null, contractorId: null, title: 'Test', description: 'Test', status: 'Open' }],
  ] satisfies [ResourceKind, object][])('validates the %s DTO', (kind, fields) => {
    expect(isResource(kind, organizationId, { id, organizationId, ...fields })).toBe(true)
    expect(isResource(kind, organizationId, { id, organizationId: otherOrganization, ...fields })).toBe(false)
  })
  it('rejects impossible dates and nonfinite money', () => {
    expect(isResource('leases', organizationId, { id, organizationId, resourceType: 'lease', tenantId: id, unitId: id, startDate: '2026-02-30', endDate: '2026-12-31', monthlyRent: 1, currency: 'CAD', status: 'Draft' })).toBe(false)
    expect(isResource('payments', organizationId, { id, organizationId, resourceType: 'payment', leaseId: id, amount: Infinity, currency: 'CAD', status: 'Pending' })).toBe(false)
  })
  it('accepts zero and beyond-end pages without inventing totals', () => {
    expect(isPage({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 }, guard)).toBe(true)
    expect(isPage({ items: [], page: 3, pageSize: 20, totalCount: 21, totalPages: 2 }, guard, { page: 3 })).toBe(true)
  })
  it.each([
    { items: [property], page: 1, pageSize: 20, totalCount: 1, totalPages: 2 },
    { items: [], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 },
    { items: [property], page: 2, pageSize: 20, totalCount: 1, totalPages: 1 },
    { items: [], page: 1, pageSize: 20, totalCount: -1, totalPages: 0 },
    { items: [], page: 1, pageSize: 20, totalCount: Number.MAX_SAFE_INTEGER + 1, totalPages: 0 },
  ])('rejects inconsistent page metadata: %j', (body) => { expect(isPage(body, guard)).toBe(false) })
})
describe('resource calls', () => {
  it('sends explicit organization scope on collections and validates ID on reads', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(Response.json({ items: [property], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 }))
      .mockResolvedValueOnce(Response.json(property))
    vi.stubGlobal('fetch', fetchMock)
    expect((await listResources('properties', organizationId)).items).toEqual([property])
    expect(fetchMock.mock.calls[0][0]).toBe('/api/v1/properties?page=1&pageSize=20')
    expect(fetchMock.mock.calls[0][1].headers).toHaveProperty('x-organization-id', organizationId)
    expect(await readResource('properties', organizationId, id)).toEqual(property)
  })
  it('rejects invalid identifiers before network activity and foreign responses after it', async () => {
    const fetchMock = vi.fn().mockResolvedValue(Response.json({ ...property, organizationId: otherOrganization }))
    vi.stubGlobal('fetch', fetchMock)
    await expect(listResources('properties', 'invalid')).rejects.toThrow('organization')
    await expect(readResource('properties', organizationId, 'invalid')).rejects.toThrow('identifiers')
    expect(fetchMock).not.toHaveBeenCalled()
    await expect(readResource('properties', organizationId, id)).rejects.toThrow('unexpected response')
  })
  it('rejects duplicate IDs and items not matching the requested exact filter', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(Response.json({ items: [property, property], page: 1, pageSize: 20, totalCount: 2, totalPages: 1 }))
      .mockResolvedValueOnce(Response.json({ items: [property], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 })))
    await expect(listResources('properties', organizationId)).rejects.toThrow('unexpected response')
    await expect(listResources('properties', organizationId, { status: 'Inactive' })).rejects.toThrow('unexpected response')
  })
})
