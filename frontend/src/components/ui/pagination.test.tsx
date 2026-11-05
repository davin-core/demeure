import { renderToStaticMarkup } from 'react-dom/server'
import { expect, it, vi } from 'vitest'
import { Pagination } from './pagination'

it('uses the server count and disables unavailable next/previous actions', () => {
  const html = renderToStaticMarkup(<Pagination page={1} pageSize={20} totalCount={0} onPageChange={vi.fn()} />)
  expect(html).toContain('No results')
  expect(html.match(/disabled=""/g)).toHaveLength(2)
  expect(html).toContain('aria-label="Pagination"')
})
it('handles a partial last page and an out-of-range page explicitly', () => {
  expect(renderToStaticMarkup(<Pagination page={2} pageSize={20} totalCount={21} onPageChange={vi.fn()} />)).toContain('Page 2 of 2 / 21 results')
  const beyond = renderToStaticMarkup(<Pagination page={3} pageSize={20} totalCount={21} onPageChange={vi.fn()} />)
  expect(beyond).toContain('Page 3 is beyond the last page (2)')
  expect(beyond.match(/disabled=""/g)).toHaveLength(1)
})
it('disables both controls while fetching and rejects invalid bounds', () => {
  expect(renderToStaticMarkup(<Pagination page={2} pageSize={20} totalCount={100} loading onPageChange={vi.fn()} />).match(/disabled=""/g)).toHaveLength(2)
  expect(() => renderToStaticMarkup(<Pagination page={0} pageSize={20} totalCount={100} onPageChange={vi.fn()} />)).toThrow('Invalid pagination')
})
