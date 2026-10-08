import { createContext, useContext } from 'react'

export interface Session {
  token: string
  userId: string
  email: string
  displayName: string
  isGuest: boolean
  expiresAt: string
}

export interface AuthContextValue {
  session: Session | null
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, displayName?: string) => Promise<void>
  /** Sample board for visitors starting fresh; none for guests arriving through a share link. */
  startGuest: (withSampleBoard?: boolean) => Promise<void>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>')
  return context
}
