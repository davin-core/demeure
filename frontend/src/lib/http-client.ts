import { ApiError, type FieldErrors } from './api-error'

export function getApiBaseUrl() {
  const base = (import.meta.env.VITE_API_BASE_URL ?? '/api').replace(/\/+$/, '')
  if (!base) throw new ApiError('VITE_API_BASE_URL must identify the API path.')
  const parsed = new URL(base, 'http://localhost')
  if (!['http:', 'https:'].includes(parsed.protocol) || parsed.username || parsed.password || parsed.search || parsed.hash
    || base.startsWith('//') || (!base.startsWith('/') && !/^https?:\/\//.test(base))
    || /\/v\d+$/.test(parsed.pathname) && !parsed.pathname.endsWith('/v1')) {
    throw new ApiError('VITE_API_BASE_URL must be an HTTP(S) API path without credentials, query or fragment, using v1 when versioned.')
  }
  return base
}

export function apiUrl(path: string, versioned = true) {
  if (!/^[a-z0-9][a-z0-9/-]*$/i.test(path)) throw new ApiError('Invalid API resource path.')
  const base = getApiBaseUrl().replace(/\/v1$/, '')
  return `${base}${versioned ? '/v1' : ''}/${path}`
}

async function json(response: Response): Promise<unknown> {
  try { return await response.json() } catch (error) {
    if (!(error instanceof SyntaxError)) throw error
    throw new ApiError('The API returned invalid JSON.', response.status, { cause: error })
  }
}

async function failure(response: Response) {
  const errors: FieldErrors = {}
  let traceId: string | undefined
  const message = `API request failed (HTTP ${response.status}).`
  if (response.headers.get('Content-Type')?.includes('application/problem+json')) {
    const body = await json(response)
    if (typeof body !== 'object' || body === null) throw new ApiError('The API returned invalid Problem Details.', response.status)
    if ('traceId' in body && typeof body.traceId === 'string') traceId = body.traceId
    if ('errors' in body) {
      if (typeof body.errors !== 'object' || body.errors === null || Array.isArray(body.errors)) throw new ApiError('The API returned invalid validation errors.', response.status)
      for (const [key, values] of Object.entries(body.errors)) {
        if (!Array.isArray(values) || !values.every((value) => typeof value === 'string')) throw new ApiError('The API returned invalid validation errors.', response.status)
        errors[key] = values
      }
    }
    const title = 'title' in body && typeof body.title === 'string' ? body.title : message
    // Unexpected server details must not be rendered as user-facing diagnostics.
    throw new ApiError(response.status >= 500 ? message : title, response.status, {
      errors, traceId, retryAfter: response.headers.get('Retry-After') ?? undefined,
    })
  }
  throw new ApiError(message, response.status, { retryAfter: response.headers.get('Retry-After') ?? undefined })
}

export type RequestOptions = {
  signal?: AbortSignal; organizationId?: string; query?: URLSearchParams
  anonymousProbe?: boolean; versioned?: boolean; authenticated?: boolean
}

export const sessionExpiredEvent = 'demeure:session-expired'

async function send(path: string, init: RequestInit, options: RequestOptions): Promise<Response> {
  const url = apiUrl(path, options.versioned)
  if (options.versioned !== false && typeof window !== 'undefined'
    && new URL(url, window.location.origin).origin !== window.location.origin) {
    throw new ApiError('Session requests require a same-origin API. Configure the frontend API proxy.')
  }
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (options.organizationId) headers.set('X-Organization-Id', options.organizationId)
  const signal = AbortSignal.any([...(options.signal ? [options.signal] : []), AbortSignal.timeout(10000)])
  const response = await fetch(`${url}${options.query?.size ? `?${options.query}` : ''}`, {
    ...init, headers: Object.fromEntries(headers.entries()), signal, credentials: 'same-origin', cache: 'no-store',
  })
  if (response.status === 401 && !options.anonymousProbe && options.authenticated !== false && typeof window !== 'undefined') {
    window.dispatchEvent(new Event(sessionExpiredEvent))
  }
  if (!response.ok) {
    if (response.status === 401 && options.anonymousProbe) return response
    await failure(response)
  }
  return response
}

export async function requestJson<T>(path: string, validate: (value: unknown) => value is T,
  options: RequestOptions = {}): Promise<T | null> {
  const response = await send(path, {}, options)
  if (response.status === 401 && options.anonymousProbe) return null
  if (response.status !== 200) throw new ApiError('The API returned an unexpected success status.', response.status)
  const body = await json(response)
  if (!validate(body)) throw new ApiError('The API returned an unexpected response contract.', response.status)
  return body
}

export async function requireJson<T>(path: string, validate: (value: unknown) => value is T,
  options: RequestOptions = {}): Promise<T> {
  const body = await requestJson(path, validate, options)
  if (body === null) throw new ApiError('The API returned no response.', 401)
  return body
}

export function writeApi<T>(path: string, body: unknown, expectedStatus: number,
  validate: (value: unknown) => value is T, options?: RequestOptions): Promise<T>
export function writeApi(path: string, body: unknown, expectedStatus: number,
  validate?: undefined, options?: RequestOptions): Promise<void>
export async function writeApi<T>(path: string, body: unknown, expectedStatus: number,
  validate?: (value: unknown) => value is T, options: RequestOptions = {}): Promise<T | void> {
  const csrf = await requireJson('auth/csrf', (value): value is { token: string } =>
    typeof value === 'object' && value !== null && 'token' in value && typeof value.token === 'string' && value.token.trim().length > 0, options)
  const response = await send(path, {
    method: 'POST', headers: { 'X-CSRF-TOKEN': csrf.token, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  }, options)
  if (response.status !== expectedStatus) throw new ApiError('The API returned an unexpected success status.', response.status)
  if (validate) {
    const value = await json(response)
    if (!validate(value)) throw new ApiError('The API returned an unexpected response contract.', response.status)
    return value
  }
}
