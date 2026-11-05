import { useEffect, useState, type ReactNode } from 'react'
import type { Membership, Session } from '@/lib/session-api'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { Icon } from '@/components/ui/icon'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'
import { Brand } from './brand'
import { Breadcrumbs, type BreadcrumbItem } from './breadcrumbs'
import { NotificationMenu, ProfileMenu } from './account-menus'
import { canAccessWorkspace, eligibleMemberships, workspaceDefinitions, workspaceNavigation, type WorkspaceKind, type WorkspaceNavigationItem } from './permissions'
import { useSession } from './session-context'
import { OrganizationContext } from './organization-context'

export function WorkspaceNavigation({ items, activePath, label = 'Workspace navigation', onNavigate }: {
  items: WorkspaceNavigationItem[]; activePath: string; label?: string; onNavigate?: () => void
}) {
  return <nav aria-label={label} className="space-y-1">
    {items.map((item) => item.href
      ? <a key={item.label} href={item.href} onClick={onNavigate} aria-current={item.href === activePath ? 'page' : undefined}
        className={cn('flex min-h-11 items-center rounded-md px-3 text-sm hover:bg-muted', item.href === activePath && 'bg-accent/40 font-semibold text-accent-foreground')}>{item.label}</a>
      : <div key={item.label} aria-disabled="true" title={item.description} className="flex min-h-11 items-center justify-between gap-2 rounded-md px-3 text-sm text-muted-foreground">
        <span>{item.label}</span><span className="shrink-0 text-[9px]">Coming soon</span>
      </div>)}
  </nav>
}

function MobileWorkspaceNavigation({ kind, items }: { kind: WorkspaceKind; items: WorkspaceNavigationItem[] }) {
  const [open, setOpen] = useState(false)
  const definition = workspaceDefinitions[kind]
  return <Dialog open={open} onOpenChange={setOpen}>
    <DialogTrigger asChild><Button variant="outline" size="icon" className="size-11 lg:hidden" aria-label="Open workspace navigation"><Icon name="menu" /></Button></DialogTrigger>
    <DialogContent className="top-0 left-0 h-dvh max-h-dvh w-[min(85vw,320px)] max-w-none translate-x-0 translate-y-0 rounded-none">
      <DialogTitle>{definition.title}</DialogTitle><DialogDescription>{kind === 'administration' ? 'Explicit platform navigation.' : 'Navigation for your selected organization.'}</DialogDescription>
      <div className="mt-8"><WorkspaceNavigation items={items} activePath={definition.path} label="Mobile workspace navigation" onNavigate={() => setOpen(false)} /></div>
      <a href="/public" className="mt-6 block py-3 text-sm text-primary">Public website</a>
    </DialogContent>
  </Dialog>
}

function OrganizationSelector({ kind, session, selectedId, onSelect }: {
  kind: WorkspaceKind; session: Session; selectedId?: string; onSelect: (id: string) => void
}) {
  if (kind === 'administration') return <div className="rounded-md border border-border px-3 py-2 text-xs text-muted-foreground">Platform scope <span className="sr-only">No organization impersonation</span></div>
  const eligible = eligibleMemberships(kind, session)
  return <div className="w-full min-w-0 sm:w-60">
    <label htmlFor="organization-selector" className="sr-only">Organization</label>
    <Select value={selectedId ?? ''} onValueChange={onSelect} disabled={eligible.length === 0}>
      <SelectTrigger id="organization-selector" className="[&>span:first-child]:truncate"><SelectValue placeholder="Choose an organization" /></SelectTrigger>
      <SelectContent>{session.memberships.map((membership) => <SelectItem key={membership.organizationId} value={membership.organizationId}
        disabled={!eligible.some((allowed) => allowed.organizationId === membership.organizationId)}>
        {membership.organizationName}{!eligible.some((allowed) => allowed.organizationId === membership.organizationId) && ' (different portal)'}
      </SelectItem>)}</SelectContent>
    </Select>
  </div>
}

function SessionStatus({ returnTo }: { returnTo: string }) {
  const { state, refresh } = useSession()
  if (state.status === 'loading') return <p role="status" className="text-sm text-muted-foreground">Checking your session...</p>
  if (state.status === 'error') return <Alert variant="destructive"><AlertTitle>Unable to verify access</AlertTitle><AlertDescription>{state.message}</AlertDescription><Button variant="outline" className="mt-3" onClick={() => void refresh()}>Retry session check</Button></Alert>
  if (state.status === 'anonymous') return <div className="space-y-4">
    {state.message && <p role="status" className="text-sm text-muted-foreground">{state.message}</p>}
    <h1 className="font-display text-4xl">Sign-in required.</h1>
    <p className="max-w-lg text-sm leading-relaxed text-muted-foreground">Sign in with an authorized account to access this portal.</p>
    <Button asChild><a href={`/login?returnTo=${encodeURIComponent(returnTo)}`}>Sign in</a></Button>
    <Button variant="outline" onClick={() => void refresh()}>Check session again</Button>
  </div>
  return null
}

export function WorkspaceLayout({ kind, children, breadcrumbs }: {
  kind: WorkspaceKind; children: ReactNode; breadcrumbs?: BreadcrumbItem[]
}) {
  const { state, refresh, selectedOrganizationId: selectedId, selectOrganization } = useSession()
  const definition = workspaceDefinitions[kind]
  const session = state.status === 'authenticated' ? state.session : undefined
  const candidates = session ? eligibleMemberships(kind, session) : []
  const membership = candidates.find((candidate) => candidate.organizationId === selectedId)
  const allowed = session && canAccessWorkspace(kind, session) && (kind === 'administration' || Boolean(membership))
  const items = allowed ? workspaceNavigation(kind, membership) : []

  useEffect(() => {
    const previous = document.title
    document.title = `Demeure | ${definition.title}`
    return () => { document.title = previous }
  }, [definition.title])

  return <div className="min-h-dvh bg-background text-foreground">
    <a href="#workspace-main" className="sr-only z-[70] bg-primary px-4 py-3 text-primary-foreground focus:not-sr-only focus:fixed focus:top-3 focus:left-3">Skip to content</a>
    <aside className="fixed inset-y-0 left-0 hidden w-64 flex-col overflow-y-auto border-r border-border bg-background px-5 py-7 lg:flex">
      <Brand />
      <p className="mt-12 mb-4 px-3 text-[10px] font-semibold tracking-[0.15em] text-muted-foreground uppercase">{definition.title}</p>
      {allowed && <WorkspaceNavigation items={items} activePath={definition.path} />}
      <div className="mt-auto border-t border-border pt-5">
        <a href="/public" className="flex min-h-11 items-center justify-between text-sm text-muted-foreground hover:text-primary">Public website <Icon name="arrow" className="size-4" /></a>
        <p className="mt-3 font-mono text-[10px] text-muted-foreground">DEMEURE / APPLICATION</p>
      </div>
    </aside>
    <div className="lg:pl-64">
      <header className="border-b border-border px-4 py-4 sm:px-8">
        <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-4">
          <div className="flex items-center gap-3">
            <div className="lg:hidden"><Brand /></div>
            {allowed && <MobileWorkspaceNavigation kind={kind} items={items} />}
            <div className="hidden lg:block"><Breadcrumbs items={breadcrumbs ?? [{ label: 'Demeure', href: '/public' }, { label: definition.title }]} /></div>
          </div>
          <div className="flex items-center gap-1 sm:gap-3">
            {session && <><NotificationMenu /><ProfileMenu session={session} /></>}
          </div>
          {session && (kind !== 'administration' || allowed) && <div className="w-full sm:w-auto lg:order-none">
            <OrganizationSelector kind={kind} session={session} selectedId={membership?.organizationId} onSelect={(id) => {
              void selectOrganization(id)
            }} />
          </div>}
        </div>
        <div className="mt-4 lg:hidden"><Breadcrumbs items={breadcrumbs ?? [{ label: 'Demeure', href: '/public' }, { label: definition.title }]} /></div>
      </header>
      <main id="workspace-main" tabIndex={-1} className="mx-auto max-w-7xl px-5 py-10 outline-none sm:px-8 sm:py-14">
        {!session ? <SessionStatus returnTo={definition.path} /> : allowed ? <>
          <div className="mb-8 flex flex-wrap items-center gap-2">
            <Badge variant="accent">{kind === 'administration' ? 'Platform administration' : membership?.organizationName}</Badge>
            <span className="text-xs text-muted-foreground">{scopeDescription(kind, membership)}</span>
          </div>
          {membership ? <OrganizationContext.Provider value={membership}>{children}</OrganizationContext.Provider> : children}
        </> : <div className="space-y-4">
          <Alert variant="destructive"><AlertTitle>Access unavailable</AlertTitle><AlertDescription>{selectedId
            ? 'Your selected organization no longer permits this portal. Choose an authorized organization or check your session.'
            : 'Your account does not have permission to access this portal.'}</AlertDescription></Alert>
          <Button variant="outline" onClick={() => void refresh()}>Check permissions again</Button>
          <p className="text-sm text-muted-foreground">Use your profile menu to open an authorized workspace.</p>
        </div>}
      </main>
      <footer className="mx-5 border-t border-border py-6 text-xs text-muted-foreground sm:mx-8">Demeure / {definition.title}</footer>
    </div>
  </div>
}

function scopeDescription(kind: WorkspaceKind, membership?: Membership) {
  if (kind === 'administration') return 'No automatic organization access or impersonation.'
  if (kind === 'tenant') return 'Your own tenancy records only.'
  if (kind === 'contractor') return 'Your assigned maintenance jobs only.'
  return membership?.role === 'PropertyManager' ? 'Assigned properties only.' : 'Current organization only.'
}

type LayoutProps = { children: ReactNode; breadcrumbs?: BreadcrumbItem[] }
export function LandlordLayout(props: LayoutProps) { return <WorkspaceLayout kind="landlord" {...props} /> }
export function TenantLayout(props: LayoutProps) { return <WorkspaceLayout kind="tenant" {...props} /> }
export function ContractorLayout(props: LayoutProps) { return <WorkspaceLayout kind="contractor" {...props} /> }
export function AdministrationLayout(props: LayoutProps) { return <WorkspaceLayout kind="administration" {...props} /> }
