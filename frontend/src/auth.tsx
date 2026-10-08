import { useCallback, useMemo, useState, type ReactNode } from 'react'
import { api, configureAuth, type AuthResponse } from './api'
import { AuthContext, type AuthContextValue, type Session } from './auth-context'

const STORAGE_KEY = 'liveboard.session'

function loadSession(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as Session
    return session.userId && new Date(session.expiresAt).getTime() > Date.now() ? session : null
  } catch {
    return null
  }
}

function saveSession(session: Session | null) {
  try {
    if (session) localStorage.setItem(STORAGE_KEY, JSON.stringify(session))
    else localStorage.removeItem(STORAGE_KEY)
  } catch {
    // Storage unavailable (private mode etc.); the session just won't survive a reload.
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(loadSession)

  const apply = useCallback((next: Session | null) => {
    saveSession(next)
    setSession(next)
  }, [])

  const logout = useCallback(() => apply(null), [apply])

  // Keep the API client's token in sync during render so child effects can call the API immediately.
  configureAuth(session?.token ?? null, logout)

  const fromResponse = useCallback(
    (r: AuthResponse) => {
      // Give the API client the token right away: callers often make a request on the very next line, before
      // React has re-rendered this provider (which is where configureAuth normally runs).
      configureAuth(r.token, logout)
      apply({ token: r.token, userId: r.userId, email: r.email, displayName: r.displayName, isGuest: r.isGuest, expiresAt: r.expiresAt })
    },
    [apply, logout],
  )

  const value = useMemo<AuthContextValue>(
    () => ({
      session,
      login: async (email, password) => fromResponse(await api.login(email, password)),
      register: async (email, password, displayName) => fromResponse(await api.register(email, password, displayName)),
      startGuest: async (withSampleBoard = true) => fromResponse(await api.guest(withSampleBoard)),
      logout,
    }),
    [session, fromResponse, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
