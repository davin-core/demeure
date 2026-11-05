import type { ComponentProps } from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { Icon } from '@/components/ui/icon'
import { cn } from '@/lib/utils'

const variants = cva('flex items-start gap-3 rounded-lg border p-4 text-sm', { variants: { variant: {
  info: 'border-info/20 bg-info-subtle text-info',
  success: 'border-success/20 bg-success-subtle text-success',
  warning: 'border-warning/20 bg-warning-subtle text-warning',
  destructive: 'border-destructive/20 bg-destructive-subtle text-destructive',
} }, defaultVariants: { variant: 'info' } })

export function Alert({ variant, className, children, role, ...props }: ComponentProps<'div'> & VariantProps<typeof variants>) {
  return <div role={role ?? (variant === 'destructive' ? 'alert' : 'status')} className={cn(variants({ variant }), className)} {...props}>
    <Icon name={variant === 'success' ? 'check' : 'info'} className="mt-0.5 size-4 shrink-0" />
    <div className="min-w-0 space-y-1">{children}</div>
  </div>
}
export function AlertTitle({ className, ...props }: ComponentProps<'p'>) {
  return <p className={cn('font-semibold', className)} {...props} />
}
export function AlertDescription({ className, ...props }: ComponentProps<'p'>) {
  return <p className={cn('text-sm leading-relaxed', className)} {...props} />
}
