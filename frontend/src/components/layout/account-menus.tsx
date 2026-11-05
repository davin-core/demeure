import type { Session } from '@/lib/session-api'
import { Button } from '@/components/ui/button'
import { Icon } from '@/components/ui/icon'
import { Disclosure } from './disclosure'
import { availableWorkspaces } from './permissions'
import { useSession } from './session-context'

export function ProfileMenu({ session }: { session: Session }) {
  const { logout } = useSession()
  const initials = session.displayName.trim().split(/\s+/).slice(0, 2).map((part) => part[0]).join('').toUpperCase()
  return <Disclosure label="User profile menu" trigger={<>
    <span aria-hidden="true" className="grid size-8 shrink-0 place-items-center rounded-full bg-accent text-xs font-semibold text-accent-foreground">{initials}</span>
    <span className="hidden max-w-36 truncate text-sm font-medium xl:inline">{session.displayName}</span>
    <Icon name="chevron" className="hidden size-3 sm:block" />
  </>}>
    <p className="break-words text-sm font-semibold">{session.displayName}</p>
    <p className="mt-1 break-all text-xs text-muted-foreground">{session.email}</p>
    <p className="mt-5 mb-2 text-[10px] font-semibold tracking-wider text-muted-foreground uppercase">Your workspaces</p>
    <nav aria-label="Available workspaces" className="space-y-1">
      {availableWorkspaces(session).map((workspace) => <a href={workspace.path} key={workspace.path} className="flex min-h-11 items-center rounded-md px-2 text-sm hover:bg-muted">{workspace.title}</a>)}
      {availableWorkspaces(session).length === 0 && <p className="text-xs text-muted-foreground">No portal permissions assigned.</p>}
    </nav>
    <div className="mt-3 border-t border-border pt-3">
      <p className="mb-3 text-xs text-muted-foreground">Profile editing is not available yet.</p>
      <Button variant="outline" className="w-full" onClick={() => void logout()}>Sign out</Button>
    </div>
  </Disclosure>
}

export function NotificationMenu() {
  return <Disclosure label="Notification menu" trigger={<Icon name="bell" />}>
    <p className="text-sm font-semibold">Notifications</p>
    <p className="mt-2 text-sm leading-relaxed text-muted-foreground">The notification feed is not available yet.</p>
    <p className="mt-3 text-xs text-muted-foreground">No unread count or notification records have been loaded.</p>
  </Disclosure>
}
