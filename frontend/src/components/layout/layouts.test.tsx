import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it, vi } from 'vitest'
import type { Membership, Session } from '@/lib/session-api'
import { Breadcrumbs } from './breadcrumbs'
import { availableWorkspaces, canAccessWorkspace, eligibleMemberships, membershipAllows, workspaceNavigation, type WorkspaceKind } from './permissions'
import { PublicLayout } from './public-layout'
import { SessionContext, useSession, type SessionState } from './session-context'
import { useOrganization } from './organization-context'
import { WorkspaceLayout, WorkspaceNavigation } from './workspace-layout'

const membership: Membership = { organizationId: '22222222-2222-4222-8222-222222222222', organizationName: 'Test organization', role: 'OrganizationOwner' }
const session: Session = { userId: '11111111-1111-4111-8111-111111111111', email: 'test@example.test', displayName: 'Test User', globalRoles: [], memberships: [membership] }
function renderWorkspace(kind: WorkspaceKind, state: SessionState) {
  const selectedOrganizationId = state.status === 'authenticated' ? eligibleMemberships(kind, state.session)[0]?.organizationId : undefined
  return renderToStaticMarkup(<SessionContext.Provider value={{ state, refresh: vi.fn(), logout: vi.fn(), selectedOrganizationId, selectOrganization: vi.fn() }}>
    <WorkspaceLayout kind={kind}><p>Protected child content</p></WorkspaceLayout>
  </SessionContext.Provider>)
}

describe('permission-aware layouts', () => {
  const roles = ['OrganizationOwner', 'PropertyManager', 'Tenant', 'Contractor', '', 'SuperAdministrator', 'unknown']
  it.each(roles)('uses current organization membership role %s, not a generic role bypass', (role) => {
    const current = { ...membership, role }
    expect(membershipAllows('landlord', current)).toBe(['OrganizationOwner', 'PropertyManager'].includes(role))
    expect(membershipAllows('tenant', current)).toBe(role === 'Tenant')
    expect(membershipAllows('contractor', current)).toBe(role === 'Contractor')
    expect(membershipAllows('administration', current)).toBe(false)
  })
  it('requires the exact global SuperAdministrator role for administration', () => {
    expect(canAccessWorkspace('administration', session)).toBe(false)
    expect(canAccessWorkspace('administration', { ...session, globalRoles: ['SuperAdministrator'] })).toBe(true)
    expect(canAccessWorkspace('administration', { ...session, globalRoles: ['OrganizationOwner'] })).toBe(false)
  })
  it('never grants ordinary organization access to a platform-only administrator', () => {
    const admin = { ...session, globalRoles: ['SuperAdministrator'], memberships: [] }
    expect(availableWorkspaces(admin).map((item) => item.path)).toEqual(['/administration'])
    expect(canAccessWorkspace('landlord', admin)).toBe(false)
  })
  it('filters multiple memberships by the requested portal without leaking another role', () => {
    const mixed = { ...session, memberships: [membership, { ...membership, organizationId: '33333333-3333-4333-8333-333333333333', role: 'Tenant' }] }
    expect(eligibleMemberships('landlord', mixed)).toEqual([membership])
    expect(availableWorkspaces(mixed).map((item) => item.path)).toEqual(['/app/dashboard', '/tenant'])
  })
  it('limits navigation to the supported resource scopes', () => {
    const manager = workspaceNavigation('landlord', { ...membership, role: 'PropertyManager' })
    expect(manager.filter((item) => !item.href).every((item) => item.description === 'Assigned properties only')).toBe(true)
    expect(workspaceNavigation('tenant', { ...membership, role: 'Tenant' }).map((item) => item.label)).not.toContain('Properties')
    expect(workspaceNavigation('contractor', { ...membership, role: 'Contractor' }).map((item) => item.label)).toEqual(['Workspace', 'Assigned jobs'])
    expect(workspaceNavigation('tenant', membership)).toEqual([])
  })
  it('disabled business entries have no invented destinations or tab stops', () => {
    const html = renderToStaticMarkup(<WorkspaceNavigation items={workspaceNavigation('landlord', membership)} activePath="/landlord" />)
    expect(html.match(/href=/g)).toHaveLength(1)
    expect(html).toContain('aria-disabled="true"')
    expect(html).toContain('Coming soon')
    expect(html).not.toContain('/landlord/properties')
  })
  it.each(['loading', 'anonymous', 'error'] as const)('does not mount protected children or navigation while %s', (status) => {
    const state: SessionState = status === 'error' ? { status, message: 'Offline' } : { status }
    const html = renderWorkspace('landlord', state)
    expect(html).not.toContain('Protected child content')
    expect(html).not.toContain('aria-label="Workspace navigation"')
    if (status === 'anonymous') expect(html).toContain('Sign-in required.')
    if (status === 'error') expect(html).toContain('Unable to verify access')
  })
  it('denies changing the layout URL to another portal', () => {
    const html = renderWorkspace('tenant', { status: 'authenticated', session })
    expect(html).toContain('Access unavailable')
    expect(html).not.toContain('Protected child content')
    expect(html).not.toContain('My leases')
  })
  it('never silently substitutes a different organization for a revoked selection', () => {
    const html = renderToStaticMarkup(<SessionContext.Provider value={{
      state: { status: 'authenticated', session }, refresh: vi.fn(), logout: vi.fn(), selectOrganization: vi.fn(),
      selectedOrganizationId: '33333333-3333-4333-8333-333333333333',
    }}><WorkspaceLayout kind="landlord">Protected child content</WorkspaceLayout></SessionContext.Provider>)
    expect(html).toContain('Access unavailable')
    expect(html).not.toContain('Protected child content')
  })
  it('provides only the selected authorized membership to organization-scoped children', () => {
    const second = { ...membership, organizationId: '33333333-3333-4333-8333-333333333333', organizationName: 'Second test organization', role: 'PropertyManager' }
    function ScopedChild() {
      const organization = useOrganization()
      return <p data-organization={organization.organizationId}>{organization.role}</p>
    }
    const html = renderToStaticMarkup(<SessionContext.Provider value={{
      state: { status: 'authenticated', session: { ...session, memberships: [membership, second] } },
      refresh: vi.fn(), logout: vi.fn(), selectOrganization: vi.fn(), selectedOrganizationId: second.organizationId,
    }}><WorkspaceLayout kind="landlord"><ScopedChild /></WorkspaceLayout></SessionContext.Provider>)
    expect(html).toContain(`data-organization="${second.organizationId}"`)
    expect(html).not.toContain(`data-organization="${membership.organizationId}"`)
  })
  it('does not present platform scope to an unauthorized administrator URL visitor', () => {
    const html = renderWorkspace('administration', { status: 'authenticated', session })
    expect(html).not.toContain('Platform scope')
    expect(html).toContain('Access unavailable')
  })
  it.each([
    ['landlord', 'OrganizationOwner'], ['landlord', 'PropertyManager'], ['tenant', 'Tenant'], ['contractor', 'Contractor'],
  ] as const)('renders authorized %s children with a live organization selector', (kind, role) => {
    const html = renderWorkspace(kind, { status: 'authenticated', session: { ...session, memberships: [{ ...membership, role }] } })
    expect(html).toContain('Protected child content')
    expect(html).toContain('id="organization-selector"')
    expect(html).toContain('aria-label="User profile menu"')
    expect(html).toContain('aria-label="Notification menu"')
    expect(html).toContain('aria-label="Breadcrumb"')
  })
  it('administration remains explicit platform scope without an organization selector', () => {
    const html = renderWorkspace('administration', { status: 'authenticated', session: { ...session, globalRoles: ['SuperAdministrator'] } })
    expect(html).toContain('Protected child content')
    expect(html).toContain('Platform scope')
    expect(html).not.toContain('id="organization-selector"')
    expect(html).toContain('No automatic organization access or impersonation.')
  })
})

describe('public layout and reusable contracts', () => {
  it('keeps public content accessible without a session', () => {
    const html = renderToStaticMarkup(<SessionContext.Provider value={{ state: { status: 'anonymous' }, refresh: vi.fn(), logout: vi.fn(), selectOrganization: vi.fn() }}>
      <PublicLayout><h1>Public content</h1></PublicLayout>
    </SessionContext.Provider>)
    expect(html).toContain('Public content')
    expect(html).toContain('aria-label="Public navigation"')
    expect(html).not.toContain('aria-label="User profile menu"')
  })
  it('marks only the last breadcrumb current and does not invent links', () => {
    const html = renderToStaticMarkup(<Breadcrumbs items={[{ label: 'Demeure', href: '/public' }, { label: 'Organization' }, { label: 'Workspace' }]} />)
    expect(html.match(/aria-current="page"/g)).toHaveLength(1)
    expect(html.match(/href=/g)).toHaveLength(1)
    expect(html).toContain('aria-label="Breadcrumb"')
  })
  it('fails explicitly for missing session and organization providers', () => {
    function MissingSession() { useSession(); return null }
    function MissingOrganization() { useOrganization(); return null }
    expect(() => renderToStaticMarkup(<MissingSession />)).toThrow('useSession must be used within SessionProvider.')
    expect(() => renderToStaticMarkup(<MissingOrganization />)).toThrow('useOrganization requires an authorized organization layout.')
  })
})
