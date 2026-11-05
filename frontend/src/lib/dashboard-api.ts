import { ApiError } from './api-error'
import { requireJson } from './http-client'
import { guid } from './session-api'

export type DashboardQuery = { startDate: string; endDate: string; propertyId?: string }
export type DashboardPreferences = { organizationId: string; filters: DashboardQuery; draft: DashboardQuery; validation?: string }
export type DashboardMoney = { currency: string; rentalIncome: number; outstandingRent: number; expenses: number }
export type DashboardResponse = {
  organizationId: string; startDate: string; endDate: string; propertyId: string | null; generatedAt: string
  properties: { id: string; name: string }[]
  totalProperties: number; totalUnits: number; occupiedUnits: number; vacantUnits: number; unavailableUnits: number
  occupancyRate: number | null; amounts: DashboardMoney[]; pendingMaintenanceRequests: number; expiringLeaseCount: number
  expiringLeases: { id: string; propertyId: string; propertyName: string; unitNumber: string; endDate: string }[]
  months: { month: string; amounts: DashboardMoney[] }[]
  recentActivities: { id: string; kind: string; label: string; status: string; occurredAt: string }[]
}

function object(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null && !Array.isArray(value) }
function date(value: unknown): value is string {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false
  const parsed = new Date(`${value}T00:00:00Z`)
  return Number.isFinite(parsed.valueOf()) && parsed.toISOString().slice(0, 10) === value
}
function count(value: unknown): value is number { return typeof value === 'number' && Number.isSafeInteger(value) && value >= 0 }
function amount(value: unknown): value is DashboardMoney {
  return object(value) && typeof value.currency === 'string' && /^[A-Z]{3}$/.test(value.currency)
    && [value.rentalIncome, value.outstandingRent, value.expenses].every((item) => typeof item === 'number' && Number.isFinite(item) && item >= 0)
}
function amounts(value: unknown): value is DashboardMoney[] {
  return Array.isArray(value) && value.every(amount) && new Set(value.map((item) => item.currency)).size === value.length
}
function instant(value: unknown) { return typeof value === 'string' && Number.isFinite(Date.parse(value)) }

export function defaultDashboardQuery(now = new Date()): DashboardQuery {
  return { startDate: new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1)).toISOString().slice(0, 10),
    endDate: new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 0)).toISOString().slice(0, 10) }
}

export function validateDashboardQuery(query: DashboardQuery) {
  let maximumEnd = '9999-12-31'
  if (date(query.startDate) && Number(query.startDate.slice(0, 4)) <= 9994) {
    const maximum = new Date(`${query.startDate}T00:00:00Z`)
    const day = maximum.getUTCDate()
    maximum.setUTCDate(1)
    maximum.setUTCFullYear(maximum.getUTCFullYear() + 5)
    const monthEnd = new Date(maximum)
    monthEnd.setUTCMonth(monthEnd.getUTCMonth() + 1, 0)
    maximum.setUTCDate(Math.min(day, monthEnd.getUTCDate()))
    maximum.setUTCDate(maximum.getUTCDate() - 1)
    maximumEnd = maximum.toISOString().slice(0, 10)
  }
  if (!date(query.startDate) || !date(query.endDate) || query.endDate < query.startDate
    || query.endDate > maximumEnd
    || query.endDate > '9999-11-30' || query.propertyId !== undefined && !guid(query.propertyId)) {
    throw new ApiError('Choose valid dates spanning at most five calendar years and a valid optional property.')
  }
}

export function isDashboard(value: unknown, organizationId: string, query: DashboardQuery): value is DashboardResponse {
  if (!object(value) || value.organizationId !== organizationId || value.startDate !== query.startDate || value.endDate !== query.endDate
    || value.propertyId !== (query.propertyId ?? null) || !instant(value.generatedAt)
    || !['totalProperties', 'totalUnits', 'occupiedUnits', 'vacantUnits', 'unavailableUnits', 'pendingMaintenanceRequests', 'expiringLeaseCount'].every((key) => count(value[key]))
    || !count(value.totalUnits) || !count(value.occupiedUnits) || !count(value.vacantUnits) || !count(value.unavailableUnits)
    || value.occupiedUnits + value.vacantUnits + value.unavailableUnits !== value.totalUnits
    || (value.totalUnits === 0 ? value.occupancyRate !== null : typeof value.occupancyRate !== 'number' || !Number.isFinite(value.occupancyRate) || Math.abs(value.occupancyRate - Math.round(value.occupiedUnits * 10000 / value.totalUnits) / 100) > 0.010000001)
    || !amounts(value.amounts) || !Array.isArray(value.properties) || !value.properties.every((item) => object(item) && guid(item.id) && typeof item.name === 'string')
    || new Set(value.properties.map((item) => item.id)).size !== value.properties.length
    || !Array.isArray(value.months) || !value.months.every((item) => object(item) && date(item.month) && item.month.endsWith('-01') && amounts(item.amounts))
    || !Array.isArray(value.expiringLeases) || value.expiringLeases.length > 10 || !count(value.expiringLeaseCount) || value.expiringLeases.length > value.expiringLeaseCount
    || !value.expiringLeases.every((item) => object(item) && guid(item.id) && guid(item.propertyId) && typeof item.propertyName === 'string' && typeof item.unitNumber === 'string' && date(item.endDate))
    || !Array.isArray(value.recentActivities) || value.recentActivities.length > 10 || !value.recentActivities.every((item) =>
      object(item) && guid(item.id) && ['payment', 'expense', 'maintenance', 'lease'].includes(String(item.kind)) &&
      typeof item.label === 'string' && typeof item.status === 'string' && instant(item.occurredAt))) return false
  const months: string[] = []
  const month = new Date(`${query.startDate.slice(0, 7)}-01T00:00:00Z`)
  while (month.toISOString().slice(0, 10) <= query.endDate) {
    months.push(month.toISOString().slice(0, 10))
    month.setUTCMonth(month.getUTCMonth() + 1)
  }
  const receivedMonths = value.months
  return months.length === receivedMonths.length && months.every((expected, index) => receivedMonths[index].month === expected)
}

export function getDashboard(organizationId: string, query: DashboardQuery, signal?: AbortSignal) {
  if (!guid(organizationId)) throw new ApiError('An authorized organization is required.')
  validateDashboardQuery(query)
  const parameters = new URLSearchParams({ startDate: query.startDate, endDate: query.endDate })
  if (query.propertyId) parameters.set('propertyId', query.propertyId)
  return requireJson('dashboard', (value): value is DashboardResponse => isDashboard(value, organizationId, query),
    { organizationId, query: parameters, signal })
}
