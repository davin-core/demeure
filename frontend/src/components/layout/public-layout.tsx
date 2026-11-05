import { useEffect, useState, type ReactNode } from 'react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { Icon } from '@/components/ui/icon'
import { Brand } from './brand'
import { Breadcrumbs, type BreadcrumbItem } from './breadcrumbs'
import { NotificationMenu, ProfileMenu } from './account-menus'
import { availableWorkspaces } from './permissions'
import { useSession } from './session-context'

export function PublicLayout({ children, breadcrumbs = [{ label: 'Home' }] }: { children: ReactNode; breadcrumbs?: BreadcrumbItem[] }) {
  const { state, refresh } = useSession()
  const [mobileOpen, setMobileOpen] = useState(false)
  const session = state.status === 'authenticated' ? state.session : undefined
  useEffect(() => {
    const previous = document.title
    document.title = 'Demeure | Public website'
    return () => { document.title = previous }
  }, [])
  const links = [{ title: 'Home', path: '/public' }, ...(session ? availableWorkspaces(session) : [])]

  return <div className="flex min-h-dvh flex-col bg-background text-foreground">
    <a href="#public-main" className="sr-only z-[70] bg-primary px-4 py-3 text-primary-foreground focus:not-sr-only focus:fixed focus:top-3 focus:left-3">Skip to content</a>
    <header className="border-b border-border">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 px-5 py-4 sm:px-8">
        <Brand />
        <nav aria-label="Public navigation" className="hidden max-w-2xl flex-wrap gap-4 lg:flex">
          {links.map((link) => <a key={link.path} href={link.path} aria-current={link.path === '/public' ? 'page' : undefined} className="py-3 text-sm text-muted-foreground hover:text-primary">{link.title}</a>)}
        </nav>
        <div className="flex items-center gap-1 sm:gap-3">
          {session && <><NotificationMenu /><ProfileMenu session={session} /></>}
          {state.status === 'anonymous' && <Button asChild variant="outline"><a href="/login">Sign in</a></Button>}
          <Dialog open={mobileOpen} onOpenChange={setMobileOpen}>
            <DialogTrigger asChild><Button variant="outline" size="icon" className="size-11 lg:hidden" aria-label="Open public navigation"><Icon name="menu" /></Button></DialogTrigger>
            <DialogContent className="top-0 left-0 h-dvh max-h-dvh w-[min(85vw,320px)] max-w-none translate-x-0 translate-y-0 rounded-none">
              <DialogTitle>Explore Demeure</DialogTitle><DialogDescription>Public website and authorized workspaces.</DialogDescription>
              <nav aria-label="Mobile public navigation" className="mt-8 space-y-1">{links.map((link) => <a key={link.path} href={link.path}
                onClick={() => setMobileOpen(false)} aria-current={link.path === '/public' ? 'page' : undefined}
                className="flex min-h-11 items-center rounded-md px-3 text-sm hover:bg-muted">{link.title}</a>)}</nav>
            </DialogContent>
          </Dialog>
        </div>
      </div>
    </header>
    <main id="public-main" tabIndex={-1} className="mx-auto w-full max-w-7xl flex-1 px-5 py-10 outline-none sm:px-8 sm:py-14">
      <div className="mb-8"><Breadcrumbs items={breadcrumbs} /></div>
      {state.status === 'error' && <Alert variant="destructive" className="mb-8"><AlertTitle>Account navigation unavailable</AlertTitle><AlertDescription>{state.message}</AlertDescription><Button variant="outline" className="mt-3" onClick={() => void refresh()}>Retry session check</Button></Alert>}
      {state.status === 'loading' && <p role="status" className="mb-6 text-xs text-muted-foreground">Checking account navigation...</p>}
      {state.status === 'anonymous' && <p role="status" className="mb-6 text-xs text-muted-foreground">{state.message ?? 'You are browsing the public website.'}</p>}
      {children}
    </main>
    <footer className="border-t border-border">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-5 px-5 py-6 sm:px-8"><Brand />
        <p className="text-xs text-muted-foreground">Demeure / Considered by design.</p>
        <nav aria-label="Public footer" className="flex gap-5 text-xs text-muted-foreground"><a href="/">Developer workspace</a><a href="/design-system">Design system</a></nav>
      </div>
    </footer>
  </div>
}
