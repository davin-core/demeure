import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { FinancialChart } from './dashboard'
import type { DashboardResponse } from '@/lib/dashboard-api'

const data: DashboardResponse = {
  organizationId: '22222222-2222-4222-8222-222222222222', startDate: '2026-02-01', endDate: '2026-02-28', propertyId: null,
  generatedAt: '2026-02-28T12:00:00Z', properties: [], totalProperties: 0, totalUnits: 0, occupiedUnits: 0, vacantUnits: 0,
  unavailableUnits: 0, occupancyRate: null, amounts: [], pendingMaintenanceRequests: 0, expiringLeaseCount: 0,
  expiringLeases: [], months: [{ month: '2026-02-01', amounts: [] }], recentActivities: [],
}
describe('financial chart', () => {
  it('has an honest empty state instead of sample statistics', () => {
    const html = renderToStaticMarkup(<FinancialChart data={data} />)
    expect(html).toContain('No financial records')
    expect(html).not.toContain('<figure')
  })
  it('provides separate labeled currencies and exact accessible chart data', () => {
    const amounts = [{ currency: 'CAD', rentalIncome: 123.45, outstandingRent: 1, expenses: 45.67 },
      { currency: 'USD', rentalIncome: 100, outstandingRent: 0, expenses: 0 }]
    const html = renderToStaticMarkup(<FinancialChart data={{ ...data, amounts, months: [{ month: '2026-02-01', amounts }] }} />)
    expect(html).toContain('Monthly rental income and expenses in CAD')
    expect(html).toContain('Monthly rental income and expenses in USD')
    expect(html).toContain('123.45')
    expect(html).toContain('45.67')
    expect(html).toContain('View chart data')
    expect(html).toContain('2026-02')
    expect(html).not.toContain('223.45')
  })
})
