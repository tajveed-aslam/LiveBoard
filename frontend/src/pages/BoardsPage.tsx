import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError, api, type BoardSummary } from '../api'
import { useAuth } from '../auth-context'
import { Avatar } from '../components/Avatar'
import { Brand } from '../components/Brand'

function updated(iso: string): string {
  return new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

export default function BoardsPage() {
  const { session, logout } = useAuth()
  const navigate = useNavigate()
  const [boards, setBoards] = useState<BoardSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [title, setTitle] = useState('')
  const [creating, setCreating] = useState(false)

  useEffect(() => {
    api.listBoards()
      .then(setBoards)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'Could not load your boards.'))
  }, [])

  async function create(e: FormEvent) {
    e.preventDefault()
    setCreating(true)
    setError(null)
    try {
      const board = await api.createBoard(title.trim())
      navigate(`/b/${board.id}`)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not create the board.')
      setCreating(false)
    }
  }

  return (
    <div className="boards-page">
      <header className="topbar">
        <Brand to="/boards" />
        <div className="topbar-actions">
          {session && <Avatar userId={session.userId} name={session.displayName} size={30} />}
          <span className="muted small hide-mobile">{session?.displayName}</span>
          {session?.isGuest && <span className="badge">Guest</span>}
          <button
            type="button"
            className="btn btn-ghost btn-sm"
            onClick={() => {
              logout()
              navigate('/')
            }}
          >
            Sign out
          </button>
        </div>
      </header>

      <main className="boards-main">
        <div className="boards-head">
          <div>
            <h1>Your boards</h1>
            <p className="muted">Boards you own or joined through a share link.</p>
          </div>
          <form className="new-board" onSubmit={create}>
            <input
              value={title}
              maxLength={120}
              placeholder="New board name"
              aria-label="New board name"
              onChange={(e) => setTitle(e.target.value)}
            />
            <button className="btn btn-primary" disabled={creating || !title.trim()}>
              {creating ? 'Creating…' : 'Create board'}
            </button>
          </form>
        </div>

        {error && <p className="error-text" role="alert">{error}</p>}
        {!error && boards === null && <p className="muted"><span className="spinner" /> Loading…</p>}
        {boards?.length === 0 && (
          <div className="panel empty-panel">
            <h2>No boards yet</h2>
            <p className="muted">Create one above, or open a share link someone sent you.</p>
          </div>
        )}

        <ul className="board-grid">
          {boards?.map((b) => (
            <li key={b.id}>
              <Link to={`/b/${b.id}`} className="board-tile">
                <span className="board-tile-title">{b.title}</span>
                <span className="board-tile-meta">
                  <span className={`role role-${b.myRole}`}>{b.myRole === 'owner' ? 'Owner' : 'Editor'}</span>
                  <span>{b.cardCount} card{b.cardCount === 1 ? '' : 's'}</span>
                  <span>{b.memberCount} member{b.memberCount === 1 ? '' : 's'}</span>
                </span>
                <span className="muted small">Updated {updated(b.updatedAt)}</span>
              </Link>
            </li>
          ))}
        </ul>
      </main>
    </div>
  )
}
