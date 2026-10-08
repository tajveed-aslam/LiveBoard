import { useEffect, useState } from 'react'
import { Avatar } from '../components/Avatar'
import type { Activity } from './useBoard'

function ago(at: number, now: number): string {
  const s = Math.max(0, Math.round((now - at) / 1000))
  if (s < 10) return 'just now'
  if (s < 60) return `${s}s ago`
  const m = Math.round(s / 60)
  return m < 60 ? `${m}m ago` : `${Math.round(m / 60)}h ago`
}

/** Live feed of what collaborators (and you) did since the board was opened. */
export function ActivityPanel({ items, meId, onClose }: { items: Activity[]; meId: string; onClose: () => void }) {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), 15_000)
    return () => window.clearInterval(id)
  }, [])

  return (
    <aside className="activity" aria-label="Live activity">
      <div className="activity-header">
        <h2>Live activity</h2>
        <button type="button" className="icon-button" aria-label="Hide activity" onClick={onClose}>×</button>
      </div>
      {items.length === 0 ? (
        <p className="muted small activity-empty">Changes made by anyone on this board will appear here as they happen.</p>
      ) : (
        <ol className="activity-list" aria-live="polite">
          {items.map((a) => (
            <li key={a.id}>
              <Avatar userId={a.actor.userId} name={a.actor.displayName} size={26} />
              <div>
                <p>
                  <strong>{a.actor.userId === meId ? 'You' : a.actor.displayName}</strong> {a.text}
                </p>
                <span className="muted small">{ago(a.at, now)}</span>
              </div>
            </li>
          ))}
        </ol>
      )}
    </aside>
  )
}
