import { initials, userColor } from '../colors'

interface AvatarProps {
  userId: string
  /** Used for the initials. */
  name: string
  /** Hover text; defaults to the name. */
  title?: string
  size?: number
  ring?: boolean
}

export function Avatar({ userId, name, title, size = 30, ring = false }: AvatarProps) {
  return (
    <span
      className={`avatar ${ring ? 'avatar-ring' : ''}`}
      style={{ width: size, height: size, background: userColor(userId), fontSize: size * 0.4 }}
      title={title ?? name}
      aria-hidden
    >
      {initials(name)}
    </span>
  )
}

interface PresenceProps {
  users: { userId: string; displayName: string; connections: number }[]
  meId: string
  max?: number
}

/** Overlapping avatars of everyone currently viewing the board. */
export function PresenceStack({ users, meId, max = 5 }: PresenceProps) {
  const shown = users.slice(0, max)
  const extra = users.length - shown.length
  const label = users.map((u) => (u.userId === meId ? `${u.displayName} (you)` : u.displayName)).join(', ')

  return (
    <div className="presence" aria-label={`Online now: ${label}`} title={`Online now: ${label}`}>
      <span className="presence-dot" aria-hidden />
      <div className="presence-stack">
        {shown.map((u) => (
          <Avatar key={u.userId} userId={u.userId} name={u.displayName} title={u.userId === meId ? `${u.displayName} (you)` : u.displayName} ring />
        ))}
        {extra > 0 && <span className="avatar avatar-more">+{extra}</span>}
      </div>
      <span className="presence-count">{users.length} online</span>
    </div>
  )
}
