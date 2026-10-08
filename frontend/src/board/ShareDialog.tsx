import { useState } from 'react'
import { ApiError, api } from '../api'
import { Modal } from '../components/Modal'
import { copyText } from '../utils'

interface Props {
  boardId: string
  shareToken: string
  isOwner: boolean
  onClose: () => void
}

export function ShareDialog({ boardId, shareToken: initialToken, isOwner, onClose }: Props) {
  const [token, setToken] = useState(initialToken)
  const [copied, setCopied] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const link = `${window.location.origin}/join/${token}`

  async function copy() {
    if (await copyText(link)) {
      setCopied(true)
      window.setTimeout(() => setCopied(false), 1800)
    }
  }

  async function reset() {
    if (!window.confirm('Reset the link? Anyone with the old link will no longer be able to join. Existing members keep access.')) return
    setBusy(true)
    setError(null)
    try {
      setToken((await api.resetShareLink(boardId)).shareToken)
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Could not reset the link.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal title="Share this board" onClose={onClose}>
      <p className="muted">Anyone with this link can join as an editor and see every change live.</p>
      <div className="share-row">
        <input className="share-input" readOnly value={link} aria-label="Share link" onFocus={(e) => e.target.select()} />
        <button type="button" className="btn btn-primary" onClick={() => void copy()}>
          {copied ? '✓ Copied' : 'Copy link'}
        </button>
      </div>

      <div className="tip">
        <p>
          <strong>See it live:</strong> open the link in a <em>private window</em> or another browser. You&apos;ll join
          as a second guest, and every drag, edit and new card shows up in both windows instantly.
        </p>
        <button type="button" className="link-button" onClick={() => window.open(link, '_blank', 'noopener')}>
          Open in a new tab
        </button>
      </div>

      {isOwner && (
        <div className="share-reset">
          <span className="muted small">Shared it with the wrong people?</span>
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => void reset()} disabled={busy}>
            Reset link
          </button>
        </div>
      )}
      {error && <p className="error-text">{error}</p>}
    </Modal>
  )
}
