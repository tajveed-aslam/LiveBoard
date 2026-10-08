import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ApiError, api } from '../api'
import { useAuth } from '../auth-context'
import { PresenceStack } from '../components/Avatar'
import { Brand } from '../components/Brand'
import { InlineEdit } from '../components/InlineEdit'
import { ActivityPanel } from '../board/ActivityPanel'
import { BoardCanvas } from '../board/BoardCanvas'
import { CardEditor } from '../board/CardEditor'
import type { EditorBadge } from '../board/CardView'
import { ShareDialog } from '../board/ShareDialog'
import { useBoard, type ConnectionStatus } from '../board/useBoard'

const STATUS_LABEL: Record<ConnectionStatus, string> = {
  connecting: 'Connecting…',
  live: 'Live',
  reconnecting: 'Reconnecting…',
  offline: 'Offline',
}

export default function BoardPage() {
  const { boardId = '' } = useParams()
  const { session } = useAuth()
  const navigate = useNavigate()
  const meId = session?.userId ?? ''
  const { board, dispatch, error, status, presence, editing, activity, deletedBy, reload, setEditingCard } = useBoard(boardId)

  const [openCardId, setOpenCardId] = useState<string | null>(null)
  const [shareOpen, setShareOpen] = useState(false)
  const [activityOpen, setActivityOpen] = useState(true)
  const [toast, setToast] = useState<string | null>(null)

  const editorsByCard = useMemo(() => {
    const map: Record<string, EditorBadge[]> = {}
    for (const [userId, info] of Object.entries(editing)) {
      if (userId === meId) continue
      ;(map[info.cardId] ??= []).push({ userId, displayName: info.displayName })
    }
    return map
  }, [editing, meId])

  function showError(message: string) {
    setToast(message)
    window.setTimeout(() => setToast((t) => (t === message ? null : t)), 4000)
  }

  async function renameBoard(title: string) {
    try {
      await api.renameBoard(boardId, title)
    } catch (e) {
      showError(e instanceof ApiError ? e.message : 'Could not rename the board.')
    }
  }

  async function deleteOrLeave() {
    if (!board) return
    const owner = board.myRole === 'owner'
    const message = owner
      ? `Delete "${board.title}" for everyone? This can't be undone.`
      : `Leave "${board.title}"? You'll need the share link to come back.`
    if (!window.confirm(message)) return
    try {
      await (owner ? api.deleteBoard(boardId) : api.leave(boardId))
      navigate('/boards')
    } catch (e) {
      showError(e instanceof ApiError ? e.message : 'That did not work.')
    }
  }

  if (error && !board) {
    return (
      <div className="centered-page">
        <div className="panel empty-panel">
          <h1>{error.status === 404 ? 'Board not found' : 'Could not load this board'}</h1>
          <p className="muted">
            {error.status === 404 ? "It may have been deleted, or you haven't joined it yet. Ask the owner for the share link." : error.message}
          </p>
          <Link to="/boards" className="btn btn-primary">Back to your boards</Link>
        </div>
      </div>
    )
  }

  if (!board) {
    return <div className="centered-page"><span className="spinner" /> Loading board…</div>
  }

  const openCard = openCardId ? board.columns.flatMap((c) => c.cards).find((c) => c.id === openCardId) : undefined
  const openColumn = board.columns.find((c) => c.cards.some((x) => x.id === openCardId))

  return (
    <div className="board-page">
      <header className="board-bar">
        <div className="board-bar-left">
          <Brand compact to="/boards" />
          <span className="divider-dot" aria-hidden>/</span>
          <InlineEdit value={board.title} onSave={renameBoard} label="board" className="board-title" />
          <span className={`status status-${status}`} role="status">
            <span className="status-dot" aria-hidden /> {STATUS_LABEL[status]}
          </span>
        </div>
        <div className="board-bar-right">
          <PresenceStack users={presence} meId={meId} />
          <button type="button" className="btn btn-primary" onClick={() => setShareOpen(true)}>Share</button>
          <button
            type="button"
            className={`btn btn-ghost ${activityOpen ? 'btn-active' : ''}`}
            aria-pressed={activityOpen}
            onClick={() => setActivityOpen((o) => !o)}
          >
            Activity{activity.length > 0 && <span className="pill-count">{activity.length}</span>}
          </button>
          <button type="button" className="btn btn-ghost" onClick={() => void deleteOrLeave()}>
            {board.myRole === 'owner' ? 'Delete' : 'Leave'}
          </button>
        </div>
      </header>

      <div className="board-body">
        <main className="board-canvas" aria-label={`${board.title} board`}>
          <BoardCanvas
            board={board}
            dispatch={dispatch}
            editorsByCard={editorsByCard}
            onOpenCard={setOpenCardId}
            onError={showError}
            onResync={() => void reload()}
          />
        </main>
        {activityOpen && <ActivityPanel items={activity} meId={meId} onClose={() => setActivityOpen(false)} />}
      </div>

      {openCardId && (
        <CardEditor
          key={openCardId}
          boardId={board.id}
          card={openCard}
          columnTitle={openColumn?.title ?? ''}
          otherEditors={editorsByCard[openCardId] ?? []}
          onClose={() => setOpenCardId(null)}
          setEditingCard={setEditingCard}
        />
      )}
      {shareOpen && (
        <ShareDialog boardId={board.id} shareToken={board.shareToken} isOwner={board.myRole === 'owner'} onClose={() => setShareOpen(false)} />
      )}

      {deletedBy && (
        <div className="overlay" role="alertdialog" aria-label="Board deleted">
          <div className="panel empty-panel">
            <h1>This board was deleted</h1>
            <p className="muted">{deletedBy.userId === meId ? 'You' : deletedBy.displayName} deleted it just now.</p>
            <Link to="/boards" className="btn btn-primary">Back to your boards</Link>
          </div>
        </div>
      )}

      {toast && <div className="toast" role="alert">{toast}</div>}
    </div>
  )
}
