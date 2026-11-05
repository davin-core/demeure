import { requestJson, writeApi } from './http-client'

export type Membership = { organizationId: string; organizationName: string; role: string }
export type Session = { userId: string; email: string; displayName: string; globalRoles: string[]; memberships: Membership[] }

function nonempty(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0
}

export function guid(value: unknown): value is string {
  return typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
    && value !== '00000000-0000-0000-0000-000000000000'
}

function isMembership(value: unknown): value is Membership {
  return typeof value === 'object' && value !== null && 'organizationId' in value && guid(value.organizationId)
    && 'organizationName' in value && nonempty(value.organizationName) && 'role' in value && typeof value.role === 'string'
}

export function isSession(value: unknown): value is Session {
  return typeof value === 'object' && value !== null
    && 'userId' in value && guid(value.userId) && 'email' in value && nonempty(value.email)
    && 'displayName' in value && nonempty(value.displayName)
    && 'globalRoles' in value && Array.isArray(value.globalRoles) && value.globalRoles.every(nonempty)
    && 'memberships' in value && Array.isArray(value.memberships) && value.memberships.every(isMembership)
    && new Set(value.memberships.map((membership) => membership.organizationId.toLowerCase())).size === value.memberships.length
}

export async function getSession(signal?: AbortSignal): Promise<Session | null> {
  return requestJson('auth/session', isSession, { signal, anonymousProbe: true })
}

export async function logoutSession(signal?: AbortSignal): Promise<void> {
  await writeApi('auth/logout', undefined, 204, undefined, { signal })
}
