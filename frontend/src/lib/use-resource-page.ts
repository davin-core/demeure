import { useEffect, useState } from 'react'
import { useOrganization } from '@/components/layout/organization-context'
import { ApiError } from './api-error'
import { listResources, type CollectionQuery, type PagedResponse, type ResourceKind, type ResourceMap } from './resource-api'

type PageState<T> = { status: 'loading' } | { status: 'error'; error: Error } | { status: 'loaded'; data: PagedResponse<T> }

export function useResourcePage<K extends ResourceKind>(kind: K, query: CollectionQuery = {}) {
  const { organizationId } = useOrganization()
  const { page = 1, pageSize = 20, status, sortBy, sortDirection } = query
  const key = JSON.stringify([kind, organizationId, page, pageSize, status, sortBy, sortDirection])
  const [retryCount, setRetryCount] = useState(0)
  const [result, setResult] = useState<{ key: string; retry: number; state: PageState<ResourceMap[K]> }>()
  useEffect(() => {
    const controller = new AbortController()
    listResources(kind, organizationId, { page, pageSize, status, sortBy, sortDirection }, controller.signal)
      .then((data) => { if (!controller.signal.aborted) setResult({ key, retry: retryCount, state: { status: 'loaded', data } }) })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) setResult({ key, retry: retryCount, state: { status: 'error', error: error instanceof Error ? error : new ApiError('Unable to load resources.') } })
      })
    return () => controller.abort()
  }, [key, kind, organizationId, page, pageSize, status, sortBy, sortDirection, retryCount])
  const state: PageState<ResourceMap[K]> = result?.key === key && result.retry === retryCount ? result.state : { status: 'loading' }
  return { state, retry: () => setRetryCount((count) => count + 1) }
}
