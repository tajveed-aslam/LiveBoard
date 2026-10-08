import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ApiError, api } from '../api'
import { useAuth } from '../auth-context'
import { Brand } from '../components/Brand'

/** /join/:token — signs the visitor in (as a guest if they like), joins the board, then opens it. */
export default function JoinPage() {
  const { token = '' } = useParams()
  const { session, startGuest } = useAuth()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const joining = useRef(false)

  useEffect(() => {
    if (!session || joining.current) return
    joining.current = true
    api.join(token)
      .then((r) => navigate(`/b/${r.boardId}`, { replace: true }))
      .catch((e) => {
        joining.current = false
        setError(e instanceof ApiError ? e.message : 'Could not join this board.')
      })
  }, [session, token, navigate])

  async function joinAsGuest() {
    setBusy(true)
    setError(null)
    try {
      await startGuest(false) // the effect above takes over once the session exists
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Could not start a guest session.')
      setBusy(false)
    }
  }

  return (
    <div className="centered-page">
      <div className="panel join-panel">
        <Brand />
        {error ? (
          <>
            <h1>Can&apos;t open this board</h1>
            <p className="error-text">{error}</p>
            <Link to={session ? '/boards' : '/'} className="btn btn-primary">{session ? 'Your boards' : 'Home'}</Link>
          </>
        ) : session ? (
          <p className="muted"><span className="spinner" /> Joining the board…</p>
        ) : (
          <>
            <h1>You&apos;ve been invited to a board</h1>
            <p className="muted">Join to see the board and everyone&apos;s changes as they happen.</p>
            <button type="button" className="btn btn-primary btn-lg" onClick={() => void joinAsGuest()} disabled={busy}>
              {busy ? <><span className="spinner" /> Joining…</> : 'Join as a guest'}
            </button>
            <Link to="/login" state={{ from: `/join/${token}` }} className="btn btn-ghost">I have an account</Link>
          </>
        )}
      </div>
    </div>
  )
}
