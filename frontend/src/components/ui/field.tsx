import { useId, type ReactNode } from 'react'
import { cn } from '@/lib/utils'

type FieldControlProps = { id: string; 'aria-describedby'?: string; 'aria-invalid': boolean }

export function Field({ label, description, error, required, children, className }: {
  label: string; description?: string; error?: string; required?: boolean; className?: string
  children: (props: FieldControlProps) => ReactNode
}) {
  const id = useId()
  const descriptionId = `${id}-description`
  const errorId = `${id}-error`
  return <div className={cn('space-y-2', className)}>
    <label htmlFor={id} className="block text-sm font-medium">{label}
      {required && <span className="ml-1 text-destructive" aria-hidden="true">*</span>}
    </label>
    {children({ id, 'aria-invalid': Boolean(error),
      'aria-describedby': [description && descriptionId, error && errorId].filter(Boolean).join(' ') || undefined })}
    {description && <p id={descriptionId} className="text-xs leading-relaxed text-muted-foreground">{description}</p>}
    {error && <p id={errorId} role="alert" className="text-xs text-destructive">{error}</p>}
  </div>
}
