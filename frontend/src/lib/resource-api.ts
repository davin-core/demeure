import { ApiError } from './api-error'
import { requireJson } from './http-client'
import { guid } from './session-api'

type BaseResource = { id: string; organizationId: string; status: string }
export type ResourceMap = {
  properties: BaseResource & { resourceType: 'property'; name: string; city: string }
  units: BaseResource & { resourceType: 'unit'; propertyId: string; number: string }
  tenants: BaseResource & { resourceType: 'tenant'; firstName: string; lastName: string }
  'rental-applications': BaseResource & { resourceType: 'rentalApplication'; tenantId: string; unitId: string }
  leases: BaseResource & { resourceType: 'lease'; tenantId: string; unitId: string; startDate: string; endDate: string; monthlyRent: number; currency: string }
  payments: BaseResource & { resourceType: 'payment'; leaseId: string; amount: number; currency: string }
  'maintenance-requests': BaseResource & { resourceType: 'maintenance'; propertyId: string; tenantId: string | null; contractorId: string | null; title: string; description: string }
}
export type ResourceKind = keyof ResourceMap
export type PagedResponse<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export type CollectionQuery = { page?: number; pageSize?: number; status?: string; sortBy?: 'id' | 'createdAt' | 'status'; sortDirection?: 'asc' | 'desc' }

const contracts: Record<ResourceKind, { discriminator: string; fields: Record<string, (value: unknown) => boolean>; statuses: string[] }> = {
  properties: { discriminator: 'property', fields: { name: text, city: text }, statuses: ['Active', 'Inactive', 'Archived'] },
  units: { discriminator: 'unit', fields: { propertyId: guid, number: text }, statuses: ['Available', 'Occupied', 'Unavailable', 'Archived'] },
  tenants: { discriminator: 'tenant', fields: { firstName: text, lastName: text }, statuses: ['Active', 'Inactive', 'Archived'] },
  'rental-applications': { discriminator: 'rentalApplication', fields: { tenantId: guid, unitId: guid }, statuses: ['Draft', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'Withdrawn'] },
  leases: { discriminator: 'lease', fields: { tenantId: guid, unitId: guid, startDate: date, endDate: date, monthlyRent: money, currency }, statuses: ['Draft', 'Active', 'Ended', 'Terminated', 'Cancelled'] },
  payments: { discriminator: 'payment', fields: { leaseId: guid, amount: money, currency }, statuses: ['Pending', 'Completed', 'Failed', 'Cancelled', 'Refunded'] },
  'maintenance-requests': { discriminator: 'maintenance', fields: { propertyId: guid, tenantId: optionalGuid, contractorId: optionalGuid, title: text, description: text }, statuses: ['Open', 'Assigned', 'InProgress', 'Completed', 'Cancelled'] },
}

function text(value: unknown) { return typeof value === 'string' }
function money(value: unknown) { return typeof value === 'number' && Number.isFinite(value) && value >= 0 }
function currency(value: unknown) { return typeof value === 'string' && /^[A-Z]{3}$/.test(value) }
function optionalGuid(value: unknown) { return value === null || guid(value) }
function date(value: unknown) {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false
  const parsed = new Date(`${value}T00:00:00Z`)
  return !Number.isNaN(parsed.valueOf()) && parsed.toISOString().slice(0, 10) === value
}

export function isResource<K extends ResourceKind>(kind: K, organizationId: string, value: unknown): value is ResourceMap[K] {
  if (typeof value !== 'object' || value === null) return false
  const body: Record<string, unknown> = Object.fromEntries(Object.entries(value))
  const contract = contracts[kind]
  return body.resourceType === contract.discriminator && guid(body.id) && guid(body.organizationId)
    && body.organizationId.toLowerCase() === organizationId.toLowerCase()
    && typeof body.status === 'string' && contract.statuses.includes(body.status)
    && Object.entries(contract.fields).every(([key, validate]) => key in body && validate(body[key]))
}

export function collectionParameters(kind: ResourceKind, query: CollectionQuery = {}) {
  const page = query.page ?? 1
  const pageSize = query.pageSize ?? 20
  if (!Number.isSafeInteger(page) || page < 1 || !Number.isSafeInteger(pageSize) || pageSize < 1 || pageSize > 100 || (page - 1) * pageSize > 2147483647) {
    throw new ApiError('Invalid pagination: use a positive page and pageSize between 1 and 100.')
  }
  if (query.status !== undefined && !contracts[kind].statuses.includes(query.status)) throw new ApiError('Invalid status filter.')
  if (query.sortBy !== undefined && !['id', 'createdAt', 'status'].includes(query.sortBy)) throw new ApiError('Invalid sort field.')
  if (query.sortDirection !== undefined && !['asc', 'desc'].includes(query.sortDirection)) throw new ApiError('Invalid sort direction.')
  const parameters = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (query.status !== undefined) parameters.set('status', query.status)
  if (query.sortBy !== undefined) parameters.set('sortBy', query.sortBy)
  if (query.sortDirection !== undefined) parameters.set('sortDirection', query.sortDirection)
  return parameters
}

export function isPage<T>(value: unknown, itemGuard: (value: unknown) => value is T, query: CollectionQuery = {}): value is PagedResponse<T> {
  if (typeof value !== 'object' || value === null || !('items' in value) || !Array.isArray(value.items)
    || !('page' in value) || value.page !== (query.page ?? 1) || !('pageSize' in value) || value.pageSize !== (query.pageSize ?? 20)
    || !('totalCount' in value) || typeof value.totalCount !== 'number' || !Number.isSafeInteger(value.totalCount) || value.totalCount < 0
    || !('totalPages' in value) || value.totalPages !== Math.ceil(value.totalCount / value.pageSize)) return false
  const expectedItems = Math.max(0, Math.min(value.pageSize, value.totalCount - (value.page - 1) * value.pageSize))
  return value.items.length === expectedItems && value.items.every(itemGuard)
}

export async function listResources<K extends ResourceKind>(kind: K, organizationId: string, query: CollectionQuery = {}, signal?: AbortSignal): Promise<PagedResponse<ResourceMap[K]>> {
  if (!guid(organizationId)) throw new ApiError('A valid organization identifier is required.')
  const parameters = collectionParameters(kind, query)
  return requireJson(kind, (value): value is PagedResponse<ResourceMap[K]> =>
    isPage(value, (item): item is ResourceMap[K] => isResource(kind, organizationId, item) && (query.status === undefined || item.status === query.status), query)
      && new Set(value.items.map((item) => item.id.toLowerCase())).size === value.items.length,
  { organizationId, query: parameters, signal })
}

export async function readResource<K extends ResourceKind>(kind: K, organizationId: string, id: string, signal?: AbortSignal): Promise<ResourceMap[K]> {
  if (!guid(organizationId) || !guid(id)) throw new ApiError('Valid organization and resource identifiers are required.')
  return requireJson(`${kind}/${id}`, (value): value is ResourceMap[K] => isResource(kind, organizationId, value) && value.id.toLowerCase() === id.toLowerCase(), { organizationId, signal })
}
