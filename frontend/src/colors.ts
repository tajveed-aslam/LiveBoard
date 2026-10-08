/** Card label colours; keys match the API's allowed values. */
export const LABEL_COLORS: { key: string; name: string; hex: string }[] = [
  { key: 'red', name: 'Red', hex: '#ef4444' },
  { key: 'orange', name: 'Orange', hex: '#f97316' },
  { key: 'yellow', name: 'Yellow', hex: '#eab308' },
  { key: 'green', name: 'Green', hex: '#22c55e' },
  { key: 'blue', name: 'Blue', hex: '#3b82f6' },
  { key: 'purple', name: 'Purple', hex: '#a855f7' },
  { key: 'pink', name: 'Pink', hex: '#ec4899' },
  { key: 'gray', name: 'Gray', hex: '#6b7280' },
]

export function labelHex(key: string | null): string | null {
  return LABEL_COLORS.find((c) => c.key === key)?.hex ?? null
}

const AVATAR_COLORS = ['#7c3aed', '#0891b2', '#db2777', '#059669', '#d97706', '#2563eb', '#dc2626', '#4f46e5']

/** Stable per-user colour, so the same person has the same avatar colour on every screen. */
export function userColor(userId: string): string {
  let hash = 0
  for (const ch of userId) hash = (hash * 31 + ch.charCodeAt(0)) >>> 0
  return AVATAR_COLORS[hash % AVATAR_COLORS.length]
}

export function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  return ((parts[0]?.[0] ?? '?') + (parts.length > 1 ? parts[parts.length - 1][0] : '')).toUpperCase()
}
