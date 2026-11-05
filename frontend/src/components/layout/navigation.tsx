import { useState, type ReactNode } from 'react'
import { Brand } from '@/components/layout/brand'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { Icon } from '@/components/ui/icon'
import { cn } from '@/lib/utils'

export type NavigationItem = { label: string; href: string }

export function Navigation({ items, activeHref, label = 'Main navigation', onNavigate }: {
  items: NavigationItem[]; activeHref?: string; label?: string; onNavigate?: () => void
}) {
  return <nav aria-label={label} className="space-y-1">
    {items.map((item, index) => <a key={item.href} href={item.href} aria-current={activeHref === item.href ? 'page' : undefined}
      onClick={onNavigate} className={cn('flex min-h-11 items-center gap-3 rounded-md px-3 text-sm no-underline transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        activeHref === item.href ? 'bg-accent/40 font-semibold text-accent-foreground' : 'text-muted-foreground')}>
      <span className="font-mono text-[10px] opacity-60">{String(index + 1).padStart(2, '0')}</span>
      {item.label}
    </a>)}
  </nav>
}

export function AppShell({ items, activeHref, children, title = 'Design system' }: {
  items: NavigationItem[]; activeHref?: string; children: ReactNode; title?: string
}) {
  const [menuOpen, setMenuOpen] = useState(false)
  return <div className="min-h-dvh bg-background text-foreground">
    <a href="#main-content" className="sr-only z-[70] rounded-md bg-primary px-4 py-3 text-primary-foreground focus:not-sr-only focus:fixed focus:top-3 focus:left-3">Skip to content</a>
    <aside className="fixed inset-y-0 left-0 hidden w-60 flex-col border-r border-border bg-background px-6 py-7 lg:flex">
      <Brand />
      <p className="mt-14 mb-4 px-3 text-[10px] font-semibold tracking-[0.15em] text-muted-foreground uppercase">The Demeure language</p>
      <Navigation items={items} activeHref={activeHref} label="Design system sections" />
      <div className="mt-auto border-t border-border pt-5">
        <a href="/" className="flex min-h-11 items-center justify-between text-sm text-muted-foreground hover:text-primary">Developer workspace <Icon name="arrow" className="size-4" /></a>
        <p className="mt-2 font-mono text-[10px] text-muted-foreground">FOUNDATION / V1.0</p>
      </div>
    </aside>
    <div className="lg:pl-60">
      <header className="flex min-h-20 items-center justify-between gap-3 border-b border-border px-5 sm:px-10">
        <div className="lg:hidden"><Brand /></div>
        <p className="hidden text-sm text-muted-foreground lg:block">Demeure <span className="mx-3 text-border">/</span><span className="text-foreground">{title}</span></p>
        <div className="flex items-center gap-4">
          <span className="hidden font-mono text-[10px] tracking-wider text-muted-foreground sm:block">COMPONENT LIBRARY / 01</span>
          <Dialog open={menuOpen} onOpenChange={setMenuOpen}>
            <DialogTrigger asChild><Button variant="outline" size="icon" className="size-11 lg:hidden" aria-label="Open navigation"><Icon name="menu" /></Button></DialogTrigger>
            <DialogContent className="top-0 left-0 h-dvh max-h-dvh w-[min(85vw,320px)] max-w-none translate-x-0 translate-y-0 rounded-none">
              <DialogTitle>Explore Demeure</DialogTitle>
              <DialogDescription>Navigate the design-system sections.</DialogDescription>
              <div className="mt-8"><Navigation items={items} activeHref={activeHref} label="Mobile design system sections" onNavigate={() => setMenuOpen(false)} /></div>
              <a href="/" className="mt-8 block text-sm text-primary underline underline-offset-4">Developer workspace</a>
            </DialogContent>
          </Dialog>
        </div>
      </header>
      <main id="main-content" tabIndex={-1} className="mx-auto max-w-[1440px] px-5 py-10 outline-none sm:px-10 sm:py-14 xl:px-16">{children}</main>
      <footer className="mx-5 flex flex-wrap justify-between gap-3 border-t border-border py-6 font-mono text-[10px] tracking-wide text-muted-foreground sm:mx-10 xl:mx-16">
        <span>DEMEURE / CONSIDERED BY DESIGN</span><span>WARM. CLEAR. BUILT TO LAST.</span>
      </footer>
    </div>
  </div>
}
