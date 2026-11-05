import { useEffect, useRef, useState, type ReactNode } from 'react'
import { cn } from '@/lib/utils'

export function Disclosure({ label, trigger, children, className }: {
  label: string; trigger: ReactNode; children: ReactNode; className?: string
}) {
  const root = useRef<HTMLDetailsElement>(null)
  const [open, setOpen] = useState(false)
  useEffect(() => {
    if (!open) return
    const closeOutside = (event: PointerEvent) => {
      if (event.target instanceof Node && !root.current?.contains(event.target) && root.current) root.current.open = false
    }
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && root.current) {
        root.current.open = false
        root.current.querySelector('summary')?.focus()
      }
    }
    document.addEventListener('pointerdown', closeOutside)
    document.addEventListener('keydown', closeOnEscape)
    return () => {
      document.removeEventListener('pointerdown', closeOutside)
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [open])
  return <details ref={root} className={cn('relative', className)} onToggle={(event) => setOpen(event.currentTarget.open)}
    onBlur={(event) => { if (event.relatedTarget instanceof Node && !event.currentTarget.contains(event.relatedTarget)) event.currentTarget.open = false }}>
    <summary aria-label={label} className="flex min-h-11 min-w-11 cursor-pointer list-none items-center justify-center gap-2 rounded-md px-2 hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring [&::-webkit-details-marker]:hidden">{trigger}</summary>
    {open && <div aria-label={label} className="fixed top-20 right-4 left-4 z-30 mt-2 max-h-[70dvh] overflow-y-auto rounded-lg border border-border bg-popover p-4 text-popover-foreground shadow-lg sm:absolute sm:top-full sm:right-0 sm:left-auto sm:w-72">{children}</div>}
  </details>
}
