import { useEffect, useRef, useState, type SubmitEvent } from 'react'
import { useOrganization } from '@/components/layout/organization-context'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Field } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api-error'
import { defaultDashboardQuery, getDashboard, validateDashboardQuery, type DashboardMoney, type DashboardPreferences, type DashboardResponse } from '@/lib/dashboard-api'

function formatMoney(value: number, currency: string) {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency, currencyDisplay: 'code' }).format(value)
}

function MoneyValues({ amounts, field }: { amounts: DashboardMoney[]; field: 'rentalIncome' | 'outstandingRent' | 'expenses' }) {
  return amounts.length ? <div className="space-y-1">{amounts.map((amount) => <p key={amount.currency} className="break-words text-xl font-semibold">{formatMoney(amount[field], amount.currency)}</p>)}</div>
    : <p className="text-sm text-muted-foreground">No recorded amounts</p>
}

function Metric({ label, value, description }: { label: string; value: React.ReactNode; description?: string }) {
  return <Card><CardHeader className="pb-3"><CardTitle className="text-xs font-medium text-muted-foreground">{label}</CardTitle></CardHeader>
    <CardContent><div className="text-3xl font-semibold tracking-tight">{value}</div>{description && <p className="mt-3 text-xs leading-relaxed text-muted-foreground">{description}</p>}</CardContent></Card>
}

export function FinancialChart({ data }: { data: DashboardResponse }) {
  const currencies = data.amounts.map((amount) => amount.currency)
  return <Card id="finances" className="scroll-mt-6"><CardHeader><CardTitle>Rental income & expenses</CardTitle><CardDescription>Completed payments by paid date; approved/paid expenses by incurred date. Partial months respect your filter.</CardDescription></CardHeader>
    <CardContent className="space-y-7">
      {currencies.length === 0 && <p className="text-sm text-muted-foreground">No financial records match this scope and period.</p>}
      {currencies.map((currency) => {
        const values = data.months.map((month) => ({ month: month.month, income: month.amounts.find((amount) => amount.currency === currency)?.rentalIncome ?? 0, expenses: month.amounts.find((amount) => amount.currency === currency)?.expenses ?? 0 }))
        const max = Math.max(1, ...values.flatMap((value) => [value.income, value.expenses]))
        return <figure key={currency} aria-label={`Monthly rental income and expenses in ${currency}`} className="space-y-3">
          <figcaption className="flex flex-wrap items-center gap-3 text-xs"><Badge>{currency}</Badge><span><span aria-hidden="true" className="mr-1 inline-block size-2 rounded-sm bg-primary" />Rental income</span><span><span aria-hidden="true" className="mr-1 inline-block size-2 rounded-sm bg-accent" />Expenses</span></figcaption>
          <div aria-hidden="true" className="flex h-40 gap-3 overflow-x-auto border-b border-border pb-2">{values.map((value) => <div key={value.month} className="flex min-w-12 flex-1 flex-col justify-end gap-2">
            <div className="flex h-28 items-end justify-center gap-1"><div title={formatMoney(value.income, currency)} className="w-3 rounded-t-sm bg-primary" style={{ height: `${value.income / max * 100}%` }} /><div title={formatMoney(value.expenses, currency)} className="w-3 rounded-t-sm bg-accent" style={{ height: `${value.expenses / max * 100}%` }} /></div>
            <span className="text-center font-mono text-[9px] text-muted-foreground">{value.month.slice(0, 7)}</span>
          </div>)}</div>
          <details><summary className="cursor-pointer py-2 text-xs text-primary">View chart data</summary>
            <Table scrollLabel={`${currency} monthly chart data`}><TableHeader><TableRow><TableHead>Month</TableHead><TableHead>Rental income</TableHead><TableHead>Expenses</TableHead></TableRow></TableHeader>
              <TableBody>{values.map((value) => <TableRow key={value.month}><TableCell>{value.month.slice(0, 7)}</TableCell><TableCell>{formatMoney(value.income, currency)}</TableCell><TableCell>{formatMoney(value.expenses, currency)}</TableCell></TableRow>)}</TableBody></Table>
          </details>
        </figure>
      })}
    </CardContent></Card>
}

function DashboardContent({ data }: { data: DashboardResponse }) {
  const period = `${data.startDate} to ${data.endDate}`
  const monthly = data.startDate.slice(0, 7) === data.endDate.slice(0, 7)
  return <div className="space-y-7">
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <Metric label="Total properties" value={data.totalProperties} description="Current non-archived properties." />
      <Metric label="Apartment units" value={data.totalUnits} description="Current non-archived units." />
      <Metric label="Occupied units" value={data.occupiedUnits} />
      <Metric label="Vacant units" value={data.vacantUnits} description={`${data.unavailableUnits} additional units unavailable.`} />
      <Metric label="Occupancy rate" value={data.occupancyRate === null ? 'Not applicable' : `${data.occupancyRate}%`} description="Current occupied / all non-archived units." />
      <Metric label={monthly ? 'Monthly rental income' : 'Rental income in period'} value={<MoneyValues amounts={data.amounts} field="rentalIncome" />} description={period} />
      <Metric label="Outstanding rent" value={<MoneyValues amounts={data.amounts} field="outstandingRent" />} description={`Recorded pending/failed payments due through ${data.endDate}.`} />
      <Metric label={monthly ? 'Monthly expenses' : 'Expenses in period'} value={<MoneyValues amounts={data.amounts} field="expenses" />} description="Approved and paid; draft/cancelled excluded." />
      <Metric label="Pending maintenance" value={data.pendingMaintenanceRequests} description="Current open, assigned or in-progress jobs." />
      <Metric label="Expiring leases" value={data.expiringLeaseCount} description={`Active leases ending within 30 days after ${data.endDate}.`} />
    </div>
    <div className="grid grid-cols-1 gap-5 xl:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
      <FinancialChart data={data} />
      <Card><CardHeader><CardTitle>Current occupancy</CardTitle><CardDescription>A current inventory snapshot, not historical occupancy.</CardDescription></CardHeader><CardContent>
        <div aria-hidden="true" className="flex h-4 overflow-hidden rounded-full bg-muted">{data.totalUnits > 0 && <>
          <div className="bg-primary" style={{ width: `${data.occupiedUnits / data.totalUnits * 100}%` }} /><div className="bg-accent" style={{ width: `${data.vacantUnits / data.totalUnits * 100}%` }} /><div className="bg-border" style={{ width: `${data.unavailableUnits / data.totalUnits * 100}%` }} />
        </>}</div>
        <dl className="mt-6 space-y-4 text-sm">{[['Occupied', data.occupiedUnits], ['Available / vacant', data.vacantUnits], ['Unavailable', data.unavailableUnits]].map(([label, count]) => <div key={label} className="flex justify-between gap-3"><dt>{label}</dt><dd className="font-semibold">{count}</dd></div>)}</dl>
        {data.totalUnits === 0 && <p className="mt-5 text-xs text-muted-foreground">No apartment units in this scope.</p>}
      </CardContent></Card>
    </div>
    <section id="alerts" aria-labelledby="dashboard-alerts" className="scroll-mt-6 space-y-4"><h2 id="dashboard-alerts" className="text-lg font-semibold">Needs your attention</h2>
      {data.amounts.some((amount) => amount.outstandingRent > 0) && <Alert variant="warning"><AlertTitle>Recorded rent remains outstanding</AlertTitle><AlertDescription>{data.amounts.filter((amount) => amount.outstandingRent > 0).map((amount) => formatMoney(amount.outstandingRent, amount.currency)).join(' / ')} due through {data.endDate}. This is not a recurring rent-charge ledger.</AlertDescription></Alert>}
      {data.pendingMaintenanceRequests > 0 && <Alert variant="info"><AlertTitle>{data.pendingMaintenanceRequests} pending maintenance {data.pendingMaintenanceRequests === 1 ? 'request' : 'requests'}</AlertTitle><AlertDescription>Open, assigned and in-progress requests still need follow-through.</AlertDescription></Alert>}
      {data.expiringLeaseCount > 0 && <Alert variant="warning"><AlertTitle>{data.expiringLeaseCount} {data.expiringLeaseCount === 1 ? 'lease' : 'leases'} approaching their end date</AlertTitle><AlertDescription>Review the upcoming lease expiries below.</AlertDescription></Alert>}
      {!data.amounts.some((amount) => amount.outstandingRent > 0) && data.pendingMaintenanceRequests === 0 && data.expiringLeaseCount === 0 && <Alert><AlertTitle>No matching attention items</AlertTitle><AlertDescription>No recorded outstanding payments, pending maintenance or upcoming expiries in this scope.</AlertDescription></Alert>}
    </section>
    <div className="grid grid-cols-1 gap-5 xl:grid-cols-2">
      <Card id="expiring-leases" className="scroll-mt-6"><CardHeader><CardTitle>Upcoming lease expiries</CardTitle><CardDescription>Earliest ten of {data.expiringLeaseCount} matching active leases.</CardDescription></CardHeader><CardContent>
        {data.expiringLeases.length ? <ul className="divide-y divide-border">{data.expiringLeases.map((lease) => <li key={lease.id} className="flex flex-wrap items-center justify-between gap-3 py-4"><div><p className="text-sm font-medium">{lease.propertyName}</p><p className="text-xs text-muted-foreground">Unit {lease.unitNumber}</p></div><time className="font-mono text-xs" dateTime={lease.endDate}>{lease.endDate}</time></li>)}</ul> : <p className="text-sm text-muted-foreground">No active leases end in the upcoming window.</p>}
      </CardContent></Card>
      <Card id="activity" className="scroll-mt-6"><CardHeader><CardTitle>Recent activity</CardTitle><CardDescription>Latest ten record creations/updates during {period}. Not a complete audit trail.</CardDescription></CardHeader><CardContent>
        {data.recentActivities.length ? <ul className="divide-y divide-border">{data.recentActivities.map((item) => <li key={`${item.kind}-${item.id}`} className="space-y-2 py-3"><div className="flex flex-wrap items-center justify-between gap-2"><p className="break-words text-sm font-medium">{item.label}</p><Badge>{item.status}</Badge></div><p className="text-xs text-muted-foreground">{item.kind} / <time dateTime={item.occurredAt}>{new Date(item.occurredAt).toLocaleString(undefined, { timeZone: 'UTC' })} UTC</time></p></li>)}</ul> : <p className="text-sm text-muted-foreground">No matching record updates in this period.</p>}
      </CardContent></Card>
    </div>
  </div>
}

type DashboardPreferenceProps = { initialPreferences?: DashboardPreferences; onPreferencesChange: (value: DashboardPreferences) => void }

export default function DashboardPage(props: DashboardPreferenceProps) {
  const { organizationId } = useOrganization()
  return <OrganizationDashboard key={organizationId} organizationId={organizationId} {...props} />
}

function OrganizationDashboard({ organizationId, initialPreferences, onPreferencesChange }: { organizationId: string } & DashboardPreferenceProps) {
  const [filters, setFilters] = useState(() => initialPreferences?.organizationId === organizationId ? initialPreferences.filters : defaultDashboardQuery())
  const [draft, setDraft] = useState(() => initialPreferences?.organizationId === organizationId ? initialPreferences.draft : filters)
  const [validation, setValidation] = useState(() => initialPreferences?.organizationId === organizationId ? initialPreferences.validation : undefined)
  useEffect(() => { onPreferencesChange({ organizationId, filters, draft, validation }) }, [onPreferencesChange, organizationId, filters, draft, validation])
  const [retry, setRetry] = useState(0)
  const [result, setResult] = useState<{ key: string; data?: DashboardResponse; error?: Error }>()
  const [choices, setChoices] = useState<{ organizationId: string; properties: DashboardResponse['properties'] }>()
  const startInput = useRef<HTMLInputElement>(null)
  const key = JSON.stringify([organizationId, filters, retry])
  useEffect(() => {
    const controller = new AbortController()
    getDashboard(organizationId, filters, controller.signal).then((data) => {
      if (!controller.signal.aborted) { setResult({ key, data }); setChoices({ organizationId, properties: data.properties }) }
    }).catch((error: unknown) => {
      if (!controller.signal.aborted) setResult({ key, error: error instanceof Error ? error : new ApiError('Unable to load the dashboard.') })
    })
    return () => controller.abort()
  }, [key, organizationId, filters])
  const current = result?.key === key ? result : undefined
  const propertyChoices = choices?.organizationId === organizationId ? choices.properties : []
  function apply(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    try { validateDashboardQuery(draft); setValidation(undefined); setFilters({ ...draft }); setRetry((value) => value + 1) }
    catch (error) { setValidation(error instanceof Error ? error.message : 'Invalid filters.'); startInput.current?.focus() }
  }
  return <div className="space-y-8">
    <div><p className="mb-4 text-[10px] font-semibold tracking-[0.15em] text-muted-foreground uppercase">Portfolio / Live overview</p><h1 className="font-display text-5xl tracking-tight">Landlord dashboard</h1>
      <p className="mt-4 max-w-2xl text-sm leading-relaxed text-muted-foreground">Current inventory, real financial records, and the next things that need your attention. Amounts stay separate by currency.</p></div>
    <Card id="dashboard-filters"><CardContent className="pt-6"><form onSubmit={apply} noValidate className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1.5fr)_auto]" aria-label="Dashboard filters">
      <Field label="Start date" required error={validation}>{(props) => <Input {...props} ref={startInput} type="date" required value={draft.startDate} onChange={(event) => setDraft({ ...draft, startDate: event.target.value })} />}</Field>
      <Field label="End date" required>{(props) => <Input {...props} type="date" required value={draft.endDate} onChange={(event) => setDraft({ ...draft, endDate: event.target.value })} />}</Field>
      <Field label="Property" className="min-w-0">{(props) => <Select value={draft.propertyId ?? 'all'} onValueChange={(value) => setDraft({ ...draft, propertyId: value === 'all' ? undefined : value })}>
        <SelectTrigger {...props} className="[&>span:first-child]:truncate"><SelectValue>{draft.propertyId ? propertyChoices.find((property) => property.id === draft.propertyId)?.name ?? 'Selected property' : 'All authorized properties'}</SelectValue></SelectTrigger><SelectContent><SelectItem value="all">All authorized properties</SelectItem>{propertyChoices.map((property) => <SelectItem key={property.id} value={property.id}>{property.name}</SelectItem>)}</SelectContent>
      </Select>}</Field>
      <Button type="submit" className="h-11 self-end">Apply filters</Button>
    </form><p className="mt-4 text-xs text-muted-foreground">UTC dates / up to five calendar years. Inventory and pending jobs are current; finances and record activity follow the date filter.</p></CardContent></Card>
    <nav aria-label="Dashboard shortcuts" className="flex flex-wrap gap-3">
      <Button variant="outline" asChild><a href="#finances">Review finances</a></Button><Button variant="outline" asChild><a href="#alerts">View alerts</a></Button><Button variant="outline" asChild><a href="#expiring-leases">Lease expiries</a></Button><Button variant="outline" asChild><a href="#activity">Recent activity</a></Button>
      <Button variant="secondary" disabled>Add property / Coming soon</Button><Button variant="secondary" disabled>Create lease / Coming soon</Button>
    </nav>
    {!current && <div role="status" aria-live="polite" className="rounded-lg border border-border bg-card p-8 text-sm text-muted-foreground">Loading your authorized dashboard...</div>}
    {current?.error && <Alert variant="destructive"><AlertTitle>Dashboard unavailable</AlertTitle><AlertDescription>{current.error.message}</AlertDescription>{current.error instanceof ApiError && current.error.traceId && <p className="text-xs">Reference: {current.error.traceId}</p>}<Button variant="outline" className="mt-3" onClick={() => setRetry((value) => value + 1)}>Retry dashboard</Button></Alert>}
    {current?.data && <><DashboardContent data={current.data} /><p className="text-xs text-muted-foreground">Last loaded: {new Date(current.data.generatedAt).toLocaleString(undefined, { timeZone: 'UTC' })} UTC</p></>}
  </div>
}
