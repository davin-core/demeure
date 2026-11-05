export type FieldErrors = Record<string, string[]>

export class ApiError extends Error {
  readonly status?: number
  readonly errors: FieldErrors
  readonly traceId?: string
  readonly retryAfter?: string

  constructor(message: string, status?: number, options?: ErrorOptions & {
    errors?: FieldErrors; traceId?: string; retryAfter?: string
  }) {
    super(message, options)
    this.name = 'ApiError'
    this.status = status
    this.errors = options?.errors ?? {}
    this.traceId = options?.traceId
    this.retryAfter = options?.retryAfter
  }
}
