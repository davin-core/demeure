import type { Membership, Session } from '@/lib/session-api'

export type WorkspaceKind = 'landlord' | 'tenant' | 'contractor' | 'administration'
export type WorkspaceNavigationItem = { label: string; href?: string; description?: string }

export const workspaceDefinitions: Record<WorkspaceKind, { title: string; path: string; description: string }> = {
  landlord: { title: 'Landlord dashboard', path: '/app/dashboard', description: 'Your organization, clearly in view.' },
  tenant: { title: 'Tenant portal', path: '/tenant', description: 'A considered space for your tenancy.' },
  contractor: { title: 'Contractor portal', path: '/contractor', description: 'A focused space for assigned work.' },
  administration: { title: 'Administration', path: '/administration', description: 'Explicit, controlled platform administration.' },
}

export function membershipAllows(kind: WorkspaceKind, membership: Membership) {
  switch (kind) {
    case 'landlord': return ['OrganizationOwner', 'PropertyManager'].includes(membership.role)
    case 'tenant': return membership.role === 'Tenant'
    case 'contractor': return membership.role === 'Contractor'
    case 'administration': return false
  }
}

export function eligibleMemberships(kind: WorkspaceKind, session: Session) {
  return session.memberships.filter((membership) => membershipAllows(kind, membership))
}

export function canAccessWorkspace(kind: WorkspaceKind, session: Session) {
  return kind === 'administration'
    ? session.globalRoles.includes('SuperAdministrator')
    : eligibleMemberships(kind, session).length > 0
}

export function availableWorkspaces(session: Session) {
  return (Object.keys(workspaceDefinitions) as WorkspaceKind[])
    .filter((kind) => canAccessWorkspace(kind, session))
    .map((kind) => workspaceDefinitions[kind])
}

export function workspaceNavigation(kind: WorkspaceKind, membership?: Membership): WorkspaceNavigationItem[] {
  const home = { label: kind === 'landlord' ? 'Dashboard' : 'Workspace', href: workspaceDefinitions[kind].path }
  if (kind === 'administration') return [home, ...['Organizations', 'Users', 'Audited platform access'].map((label) => ({ label }))]
  if (!membership || !membershipAllows(kind, membership)) return []
  switch (kind) {
    case 'landlord': return [home, ...['Properties', 'Units', 'Tenants', 'Rental applications', 'Leases', 'Payments', 'Maintenance'].map((label) => ({
      label, description: membership.role === 'PropertyManager' ? 'Assigned properties only' : 'Current organization only',
    }))]
    case 'tenant': return [home, ...['My tenancy', 'My applications', 'My leases', 'My payments', 'My maintenance'].map((label) => ({ label, description: 'Your own records only' }))]
    case 'contractor': return [home, { label: 'Assigned jobs', description: 'Your assigned maintenance only' }]
  }
}
