import { Button } from './button'
import { ApiError } from '@/lib/api-error'

export function Pagination({ page, pageSize, totalCount, loading = false, onPageChange }: {
  page: number; pageSize: number; totalCount: number; loading?: boolean; onPageChange: (page: number) => void
}) {
  if (!Number.isSafeInteger(page) || page < 1 || !Number.isSafeInteger(pageSize) || pageSize < 1 || pageSize > 100 || !Number.isSafeInteger(totalCount) || totalCount < 0) {
    throw new ApiError('Invalid pagination component parameters.')
  }
  const totalPages = Math.ceil(totalCount / pageSize)
  return <nav aria-label="Pagination" className="flex flex-wrap items-center justify-between gap-3">
    <p role="status" className="text-xs text-muted-foreground">{totalCount === 0 ? 'No results' : page > totalPages
      ? `Page ${page} is beyond the last page (${totalPages}) / ${totalCount} results`
      : `Page ${page} of ${totalPages} / ${totalCount} results`}</p>
    <div className="flex gap-2">
      <Button variant="outline" disabled={loading || page <= 1} onClick={() => onPageChange(page - 1)}>Previous</Button>
      <Button variant="outline" disabled={loading || page >= totalPages} onClick={() => onPageChange(page + 1)}>Next</Button>
    </div>
  </nav>
}
