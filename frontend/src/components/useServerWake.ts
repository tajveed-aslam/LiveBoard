import { useEffect, useState } from 'react'
import { api } from '../api'

export type ServerState = 'checking' | 'waking' | 'ready' | 'down'

/**
 * Free hosting sleeps the API when idle, and the first request can take ~a minute.
 * Ping it early and tell the visitor what's happening instead of looking broken.
 */
export function useServerWake(): ServerState {
  const [state, setState] = useState<ServerState>('checking')

  useEffect(() => {
    let cancelled = false
    const slowTimer = window.setTimeout(() => !cancelled && setState((s) => (s === 'checking' ? 'waking' : s)), 2500)

    const ping = async (attempt: number) => {
      try {
        await api.health()
        if (!cancelled) setState('ready')
      } catch {
        if (cancelled) return
        // ~2 minutes in total: a sleeping free-tier instance can take over a minute to boot.
        if (attempt >= 20) setState('down')
        else window.setTimeout(() => void ping(attempt + 1), 5000)
      }
    }
    void ping(1)

    return () => {
      cancelled = true
      window.clearTimeout(slowTimer)
    }
  }, [])

  return state
}
