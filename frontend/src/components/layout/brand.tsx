import { cn } from '@/lib/utils'

export function Brand({ className }: { className?: string }) {
  return <a href="/" aria-label="Demeure home" className={cn('inline-flex items-center gap-2.5 text-lg font-bold tracking-tight text-foreground no-underline', className)}>
    <span aria-hidden="true" className="grid size-8 place-items-center rounded-full bg-accent font-display text-lg font-normal text-accent-foreground">D</span>
    demeure
  </a>
}
