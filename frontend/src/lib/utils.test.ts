import { expect, it } from 'vitest'
import { cn } from './utils'

it('combines conditional classes and resolves conflicting Tailwind utilities', () => {
  expect(cn('px-2', false, { 'font-medium': true }, 'px-4')).toBe('font-medium px-4')
})
