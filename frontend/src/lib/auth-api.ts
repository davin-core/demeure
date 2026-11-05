import { ApiError, type FieldErrors } from './api-error'
import { requireJson, writeApi } from './http-client'
import { guid, isSession, type Session } from './session-api'

export type LoginRequest = { email: string; password: string }
export type RegisterRequest = LoginRequest & { displayName: string; organizationName: string; organizationSlug: string }
export type ResetPasswordRequest = { email: string; token: string; newPassword: string }
export type ConfirmEmailRequest = { userId: string; token: string }

export function validateLogin(value: LoginRequest): FieldErrors {
  const errors: FieldErrors = {}
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.email) || value.email.length > 254) errors.email = ['Enter a valid email address (up to 254 characters).']
  if (!value.password.trim() || value.password.length > 128) errors.password = ['Enter your password (up to 128 characters).']
  return errors
}

function validatePassword(password: string): string[] {
  return password.length < 12 || password.length > 128 || new Set(password).size < 6
    || !/[a-z]/.test(password) || !/[A-Z]/.test(password) || !/[0-9]/.test(password) || !/[^a-zA-Z0-9]/.test(password)
    ? ['Use 12-128 characters, at least six unique characters, uppercase, lowercase, a number and a symbol.'] : []
}

export function validateRegistration(value: RegisterRequest): FieldErrors {
  const errors = validateLogin(value)
  const password = validatePassword(value.password)
  if (password.length) errors.password = password
  for (const key of ['displayName', 'organizationName'] as const) {
    if (!value[key].trim() || value[key].length > 200) errors[key] = ['Enter a value up to 200 characters.']
  }
  if (value.organizationSlug.length > 100 || !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(value.organizationSlug)) errors.organizationSlug = ['Use lowercase letters, numbers and single hyphens (up to 100 characters).']
  return errors
}

export function validateResetPassword(value: ResetPasswordRequest): FieldErrors {
  const errors = validateLogin({ email: value.email, password: value.newPassword })
  delete errors.password
  const password = validatePassword(value.newPassword)
  if (password.length) errors.newPassword = password
  if (!value.token.trim() || value.token.length > 4096) errors.token = ['Provide a valid reset token.']
  return errors
}

function assertValid(errors: FieldErrors) {
  if (Object.keys(errors).length) throw new ApiError('Please correct the highlighted fields.', undefined, { errors })
}

export async function login(value: LoginRequest, signal?: AbortSignal): Promise<Session> {
  assertValid(validateLogin(value))
  await writeApi('auth/login', value, 204, undefined, { signal, authenticated: false })
  return requireJson('auth/session', isSession, { signal })
}

export async function registerOwner(value: RegisterRequest, signal?: AbortSignal) {
  assertValid(validateRegistration(value))
  return writeApi('auth/register', value, 201, (body): body is { userId: string; organizationId: string } =>
    typeof body === 'object' && body !== null && 'userId' in body && guid(body.userId) && 'organizationId' in body && guid(body.organizationId),
  { signal, authenticated: false })
}

export async function requestRecoveryEmail(kind: 'verification-email' | 'forgot-password', email: string, signal?: AbortSignal) {
  const errors = validateLogin({ email, password: 'unused' })
  assertValid(errors)
  await writeApi(`auth/${kind}`, { email }, 202, undefined, { signal, authenticated: false })
}

export async function confirmEmail(value: ConfirmEmailRequest, signal?: AbortSignal) {
  const errors: FieldErrors = {}
  if (!guid(value.userId)) errors.userId = ['Provide a valid account identifier.']
  if (!value.token.trim() || value.token.length > 4096) errors.token = ['Provide a valid verification token.']
  assertValid(errors)
  await writeApi('auth/confirm-email', value, 204, undefined, { signal, authenticated: false })
}

export async function resetPassword(value: ResetPasswordRequest, signal?: AbortSignal) {
  assertValid(validateResetPassword(value))
  await writeApi('auth/reset-password', value, 204, undefined, { signal, authenticated: false })
}

export async function logoutAll(signal?: AbortSignal) {
  await writeApi('auth/logout-all', undefined, 204, undefined, { signal })
}

export function fieldError(errors: FieldErrors, name: string) {
  return Object.entries(errors).filter(([key]) => key.toLowerCase() === name.toLowerCase()).flatMap(([, messages]) => messages).join(' ') || undefined
}
