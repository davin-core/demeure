import type { ComponentProps } from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const variants = cva('inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium', {
  variants: { variant: {
    default: 'bg-secondary text-secondary-foreground',
    accent: 'bg-accent text-accent-foreground',
    success: 'bg-success-subtle text-success',
    warning: 'bg-warning-subtle text-warning',
    destructive: 'bg-destructive-subtle text-destructive',
    info: 'bg-info-subtle text-info',
  } }, defaultVariants: { variant: 'default' },
})

export function Badge({ variant, className, ...props }: ComponentProps<'span'> & VariantProps<typeof variants>) {
  return <span className={cn(variants({ variant }), className)} {...props} />
}
