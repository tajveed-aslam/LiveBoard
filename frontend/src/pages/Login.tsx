import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../api'
import { useAuth } from '../auth-context'
import Brand from '../components/Brand'

export default function Login() {
  const { session, login, register, startGuest } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from ?? '/app'

  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (session && !session.isGuest) return <Navigate to={from} replace />

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setError(null)
    try {
      await action()
      navigate(from, { replace: true })
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Something went wrong.')
    } finally {
      setBusy(false)
    }
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault()
    void run(() => (mode === 'login' ? login(email, password) : register(email, password)))
  }

  return (
    <div className="auth-page">
      <div className="auth-card card">
        <Brand />
        <h1>{mode === 'login' ? 'Sign in' : 'Create your account'}</h1>
        <p className="muted">
          {mode === 'login' ? 'Welcome back — your analysis history is waiting.' : 'Keep every CV analysis in your own history.'}
        </p>

        <form onSubmit={onSubmit} className="stack-form">
          <label>
            Email
            <input type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
          </label>
          <label>
            Password
            <input
              type="password"
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              required
              minLength={mode === 'register' ? 8 : undefined}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            {mode === 'register' && <span className="field-hint">At least 8 characters.</span>}
          </label>
          {error && <p className="error-text" role="alert">{error}</p>}
          <button className="btn btn-primary" disabled={busy}>
            {busy ? <span className="spinner" /> : mode === 'login' ? 'Sign in' : 'Create account'}
          </button>
        </form>

        <p className="muted small">
          {mode === 'login' ? "Don't have an account? " : 'Already registered? '}
          <button className="link-button" onClick={() => { setMode(mode === 'login' ? 'register' : 'login'); setError(null) }}>
            {mode === 'login' ? 'Create one' : 'Sign in'}
          </button>
        </p>

        <div className="divider"><span>or</span></div>
        <button className="btn btn-ghost" disabled={busy} onClick={() => void run(startGuest)}>
          Continue as guest
        </button>
      </div>
    </div>
  )
}
