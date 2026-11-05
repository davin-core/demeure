import { requireJson } from './http-client'
export { ApiError } from './api-error'
export { getApiBaseUrl } from './http-client'

export type HealthResponse = {
  status: 'ok' | 'degraded' | 'unhealthy'
  service: string
}

function isHealthResponse(value: unknown): value is HealthResponse {
  return typeof value === 'object' && value !== null
    && 'status' in value && typeof value.status === 'string' && ['ok', 'degraded', 'unhealthy'].includes(value.status)
    && 'service' in value && typeof value.service === 'string' && value.service.trim().length > 0
}

export async function getHealth(signal?: AbortSignal): Promise<HealthResponse> {
  return requireJson('health', isHealthResponse, { signal, versioned: false, authenticated: false })
}
