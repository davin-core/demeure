/// <reference types="node" />
import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const styles = readFileSync(resolve(import.meta.dirname, '..', '..', 'index.css'), 'utf8')

function token(name: string) {
  const hex = styles.match(new RegExp(`--${name}:\\s*(#[0-9a-f]{6});`, 'i'))?.[1]
  if (!hex) throw new Error(`Missing color token: ${name}`)
  return hex
}

function luminance(hex: string) {
  const rgb = [1, 3, 5].map((offset) => {
    const channel = Number.parseInt(hex.slice(offset, offset + 2), 16) / 255
    return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4
  })
  return rgb[0] * 0.2126 + rgb[1] * 0.7152 + rgb[2] * 0.0722
}

function contrast(first: string, second: string) {
  const a = luminance(token(first))
  const b = luminance(token(second))
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)
}

describe('semantic color contrast', () => {
  it.each([
    ['foreground', 'background'],
    ['muted-foreground', 'background'],
    ['muted-foreground', 'muted'],
    ['card-foreground', 'card'],
    ['primary-foreground', 'primary'],
    ['accent-foreground', 'accent'],
    ['secondary-foreground', 'secondary'],
    ['destructive-foreground', 'destructive'],
    ['success', 'success-subtle'],
    ['warning', 'warning-subtle'],
    ['info', 'info-subtle'],
    ['destructive', 'destructive-subtle'],
  ])('%s text on %s meets 4.5:1', (text, surface) => {
    expect(contrast(text, surface)).toBeGreaterThanOrEqual(4.5)
  })

  it.each([
    ['input', 'card'],
    ['input', 'background'],
    ['ring', 'background'],
  ])('%s boundary against %s meets 3:1', (boundary, surface) => {
    expect(contrast(boundary, surface)).toBeGreaterThanOrEqual(3)
  })
})
