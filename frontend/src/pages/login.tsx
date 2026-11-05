import { useEffect, useRef, useState, type SubmitEvent } from 'react'
import { Brand } from '@/components/layout/brand'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Field } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { ApiError, type FieldErrors } from '@/lib/api-error'
import { fieldError, login, validateLogin } from '@/lib/auth-api'
import { getSession } from '@/lib/session-api'
import { loginDestination } from '@/lib/login-navigation'

export default function LoginPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState<FieldErrors>({})
  const [error, setError] = useState<Error | null>(null)
  const [state, setState] = useState<'checking' | 'ready' | 'submitting'>('checking')
  const [retry, setRetry] = useState(0)
  const emailRef = useRef<HTMLInputElement>(null)
  const passwordRef = useRef<HTMLInputElement>(null)
  const request = useRef<AbortController | null>(null)
  const returnTo = new URLSearchParams(window.location.search).get('returnTo')

  useEffect(() => {
    const previous = document.title
    document.title = 'Demeure | Sign in'
    const controller = new AbortController()
    getSession(controller.signal).then((session) => {
      if (controller.signal.aborted) return
      if (session) window.location.replace(loginDestination(session, returnTo))
      else setState('ready')
    }).catch((failure: unknown) => {
      if (!controller.signal.aborted) {
        setError(failure instanceof Error ? failure : new ApiError('Unable to check your session.'))
        setState('ready')
      }
    })
    return () => {
      controller.abort()
      request.current?.abort()
      document.title = previous
    }
  }, [retry, returnTo])

  async function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (request.current || state !== 'ready') return
    const value = { email: email.trim(), password }
    const validation = validateLogin(value)
    setErrors(validation)
    setError(null)
    if (Object.keys(validation).length) {
      if (validation.email) emailRef.current?.focus()
      else passwordRef.current?.focus()
      return
    }
    const controller = new AbortController()
    request.current = controller
    setState('submitting')
    try {
      const session = await login(value, controller.signal)
      if (!controller.signal.aborted) window.location.assign(loginDestination(session, returnTo))
    } catch (failure) {
      if (controller.signal.aborted) return
      const apiError = failure instanceof Error ? failure : new ApiError('Unable to sign in.')
      setError(apiError)
      setErrors(apiError instanceof ApiError ? apiError.errors : {})
      setPassword('')
      setState('ready')
      requestAnimationFrame(() => {
        if (controller.signal.aborted) return
        if (apiError instanceof ApiError && fieldError(apiError.errors, 'email')) emailRef.current?.focus()
        else passwordRef.current?.focus()
      })
    } finally {
      if (request.current === controller) request.current = null
    }
  }

  return <main className="mx-auto flex min-h-dvh max-w-lg flex-col justify-center gap-8 px-5 py-10">
    <Brand />
    <Card><CardHeader><h1 className="font-display text-4xl">Welcome home.</h1><CardTitle>Sign in to Demeure</CardTitle>
      <CardDescription>Use your verified account to open an authorized workspace.</CardDescription></CardHeader>
      <CardContent>
        {state === 'checking' && <p role="status" className="mb-5 text-sm text-muted-foreground">Checking your session...</p>}
        {error && <Alert variant="destructive" className="mb-5"><AlertTitle>{error instanceof ApiError && error.status === 401 ? 'Sign-in failed' : 'Unable to sign in'}</AlertTitle>
          <AlertDescription>{error instanceof ApiError && error.status === 401 ? 'Check your credentials and email verification. Your account may also be locked or unavailable.' : error.message}</AlertDescription>
          {error instanceof ApiError && error.status === 429 && <p className="text-xs">Too many requests. Please wait before trying again.{error.retryAfter && ` Retry-After: ${error.retryAfter}.`}</p>}
          {error instanceof ApiError && error.traceId && <p className="break-all text-xs">Reference: {error.traceId}</p>}
        </Alert>}
        <form noValidate onSubmit={submit} className="space-y-5" aria-busy={state !== 'ready'}>
          <Field label="Email address" required error={fieldError(errors, 'email')}>{(props) => <Input {...props} ref={emailRef} required type="email" maxLength={254} autoComplete="username" value={email} disabled={state !== 'ready'} onChange={(event) => setEmail(event.target.value)} />}</Field>
          <Field label="Password" required error={fieldError(errors, 'password')}>{(props) => <Input {...props} ref={passwordRef} required type="password" maxLength={128} autoComplete="current-password" value={password} disabled={state !== 'ready'} onChange={(event) => setPassword(event.target.value)} />}</Field>
          <Button type="submit" className="w-full" disabled={state !== 'ready'}>{state === 'submitting' ? 'Signing in...' : 'Sign in'}</Button>
          {state === 'submitting' && <p role="status" className="text-xs text-muted-foreground">Verifying your credentials and permissions...</p>}
        </form>
        {error && <Button variant="link" className="mt-3 px-0" disabled={state !== 'ready'} onClick={() => { setError(null); setState('checking'); setRetry((count) => count + 1) }}>Check session again</Button>}
        <p className="mt-6 text-xs leading-relaxed text-muted-foreground">Registration, verification and password-recovery screens are not available yet. Existing account API flows remain supported.</p>
      </CardContent>
    </Card>
    <a href="/public" className="text-sm text-primary underline underline-offset-4">Back to the public website</a>
  </main>
}
