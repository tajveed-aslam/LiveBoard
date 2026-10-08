import { useEffect, useRef, useState } from 'react'
import { ApiError, api, type Card } from '../api'
import { LABEL_COLORS } from '../colors'
import { Avatar } from '../components/Avatar'
import { Modal } from '../components/Modal'
import type { EditorBadge } from './CardView'

interface Props {
  boardId: string
  card: Card | undefined
  columnTitle: string
  otherEditors: EditorBadge[]
  onClose: () => void
  setEditingCard: (cardId: string | null) => void
}

/**
 * Edits one card. Collaborators see an "editing" badge while it's open; if someone else saves the same card
 * meanwhile, a banner offers to load their version instead of silently overwriting it.
 */
export function CardEditor({ boardId, card, columnTitle, otherEditors, onClose, setEditingCard }: Props) {
  const [title, setTitle] = useState(card?.title ?? '')
  const [description, setDescription] = useState(card?.description ?? '')
  const [color, setColor] = useState<string | null>(card?.color ?? null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const baseVersion = useRef(card?.updatedAt)
  const cardId = card?.id

  // Announce that we're editing this card for as long as the editor is open.
  useEffect(() => {
    if (!cardId) return
    setEditingCard(cardId)
    return () => setEditingCard(null)
  }, [cardId, setEditingCard])

  if (!card) {
    return (
      <Modal title="Card deleted" onClose={onClose}>
        <p>Someone deleted this card while you had it open.</p>
      </Modal>
    )
  }

  const changedRemotely = card.updatedAt !== baseVersion.current && !busy
  const dirty = title.trim() !== card.title || description !== card.description || color !== card.color

  function loadLatest() {
    if (!card) return
    setTitle(card.title)
    setDescription(card.description)
    setColor(card.color)
    baseVersion.current = card.updatedAt
  }

  async function save() {
    if (!card) return
    setBusy(true)
    setError(null)
    try {
      const saved = await api.updateCard(boardId, card.id, {
        title: title.trim(),
        description,
        color: color ?? '',
      })
      baseVersion.current = saved.updatedAt
      onClose()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Could not save the card.')
    } finally {
      setBusy(false)
    }
  }

  async function remove() {
    if (!card || !window.confirm(`Delete "${card.title}"?`)) return
    try {
      await api.deleteCard(boardId, card.id)
      onClose()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Could not delete the card.')
    }
  }

  return (
    <Modal title="Edit card" onClose={onClose} wide>
      <p className="muted small">In <strong>{columnTitle}</strong></p>

      {otherEditors.length > 0 && (
        <div className="notice notice-info">
          {otherEditors.map((e) => <Avatar key={e.userId} userId={e.userId} name={e.displayName} size={22} />)}
          <span>
            <strong>{otherEditors.map((e) => e.displayName).join(', ')}</strong> {otherEditors.length === 1 ? 'is' : 'are'} also editing this card.
          </span>
        </div>
      )}
      {changedRemotely && (
        <div className="notice notice-warn">
          <span>Someone else just saved changes to this card.</span>
          <button type="button" className="link-button" onClick={loadLatest}>Load their version</button>
        </div>
      )}

      <form
        className="card-form"
        onSubmit={(e) => {
          e.preventDefault()
          void save()
        }}
      >
        <label>
          Title
          <input value={title} maxLength={200} onChange={(e) => setTitle(e.target.value)} required />
        </label>
        <label>
          Description
          <textarea value={description} maxLength={5000} rows={6} onChange={(e) => setDescription(e.target.value)} placeholder="Add more detail…" />
        </label>
        <fieldset className="color-picker">
          <legend>Label</legend>
          <button type="button" className={`swatch swatch-none ${color === null ? 'selected' : ''}`} onClick={() => setColor(null)} aria-pressed={color === null}>
            None
          </button>
          {LABEL_COLORS.map((c) => (
            <button
              key={c.key}
              type="button"
              className={`swatch ${color === c.key ? 'selected' : ''}`}
              style={{ background: c.hex }}
              aria-label={c.name}
              aria-pressed={color === c.key}
              title={c.name}
              onClick={() => setColor(c.key)}
            />
          ))}
        </fieldset>

        {error && <p className="error-text" role="alert">{error}</p>}

        <div className="card-form-actions">
          <button type="button" className="btn btn-danger-ghost btn-sm" onClick={() => void remove()}>Delete card</button>
          <div className="spacer" />
          <button type="button" className="btn btn-ghost" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary" disabled={busy || !title.trim() || !dirty}>
            {busy ? 'Saving…' : 'Save'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
