import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError, api } from '../api'
import { useAuth } from '../auth-context'
import { Avatar } from '../components/Avatar'
import { Brand } from '../components/Brand'
import { useServerWake } from '../components/useServerWake'

const GITHUB_URL = 'https://github.com/tajveed-aslam/LiveBoard'

const features = [
  { icon: '⚡', title: 'Live by default', text: 'Every new card, edit and drag is pushed over SignalR to everyone on the board the moment it is saved.' },
  { icon: '🔗', title: 'Share with a link', text: 'Send a link, and anyone who opens it joins as an editor. The owner can reset it to cut off old links.' },
  { icon: '👀', title: 'See who is here', text: "Avatars show who's online, cards show who's editing them, and a live feed tracks every change." },
]

const stack = ['ASP.NET Core 8', 'SignalR', 'EF Core', 'PostgreSQL', 'JWT auth', 'React', 'TypeScript', 'dnd-kit', 'xUnit', 'Playwright']

/** Decorative mini-board for the hero. */
function BoardPreview() {
  const people = [
    { id: 'a1', name: 'Swift Otter' },
    { id: 'b2', name: 'Calm Falcon' },
    { id: 'c3', name: 'Bright Lynx' },
  ]
  return (
    <div className="preview panel" aria-hidden>
      <div className="preview-bar">
        <span className="preview-title">Launch plan</span>
        <span className="status status-live"><span className="status-dot" /> Live</span>
        <span className="presence-stack">{people.map((p) => <Avatar key={p.id} userId={p.id} name={p.name} size={26} ring />)}</span>
      </div>
      <div className="preview-columns">
        <div className="preview-col">
          <span className="preview-col-title">To do</span>
          <div className="preview-card"><span className="card-label" style={{ background: '#3b82f6' }} />Write release notes</div>
          <div className="preview-card">QA on staging</div>
        </div>
        <div className="preview-col">
          <span className="preview-col-title">In progress</span>
          <div className="preview-card preview-card-moving">
            <span className="card-label" style={{ background: '#a855f7' }} />Polish onboarding
            <span className="preview-cursor"><Avatar userId="b2" name="Calm Falcon" size={18} /> Calm Falcon</span>
          </div>
        </div>
        <div className="preview-col">
          <span className="preview-col-title">Done</span>
          <div className="preview-card"><span className="card-label" style={{ background: '#22c55e' }} />Design review</div>
        </div>
      </div>
    </div>
  )
}

export default function Landing() {
  const { session, startGuest } = useAuth()
  const navigate = useNavigate()
  const server = useServerWake()
  const [starting, setStarting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function tryDemo() {
    setStarting(true)
    setError(null)
    try {
      if (!session) await startGuest(true)
      // Straight into the sample board a new guest gets.
      const boards = await api.listBoards()
      navigate(boards.length > 0 ? `/b/${boards[0].id}` : '/boards')
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Could not start the demo.')
      setStarting(false)
    }
  }

  return (
    <div className="landing">
      <header className="topbar">
        <Brand />
        <nav className="topbar-actions">
          <a href={GITHUB_URL} target="_blank" rel="noreferrer" className="link-muted">GitHub</a>
          {session ? <Link to="/boards" className="btn btn-ghost">Your boards</Link> : <Link to="/login" className="btn btn-ghost">Sign in</Link>}
        </nav>
      </header>

      <main>
        <section className="hero">
          <div className="hero-copy">
            <p className="eyebrow">Real-time collaboration</p>
            <h1>
              A Kanban board your whole team edits <span className="accent">at the same time</span>
            </h1>
            <p className="lead">
              Drag cards between columns, share the board with a link, and watch everyone&apos;s changes appear instantly,
              with no refreshing and no &ldquo;who has the latest version?&rdquo;
            </p>
            <div className="hero-actions">
              <button type="button" className="btn btn-primary btn-lg" onClick={() => void tryDemo()} disabled={starting}>
                {starting ? <><span className="spinner" /> Opening your board…</> : session ? 'Open my boards' : 'Try the live demo'}
              </button>
              {!session && <Link to="/login" className="btn btn-ghost btn-lg">Create an account</Link>}
            </div>
            <p className="hint">
              No sign-up needed. To see real-time sync, click <strong>Share</strong> on your board and open the link in a
              private window.
            </p>
            {error && <p className="error-text">{error}</p>}
            <ServerStatus state={server} />
          </div>
          <BoardPreview />
        </section>

        <section className="features">
          {features.map((f) => (
            <article key={f.title} className="panel feature">
              <span className="feature-icon" aria-hidden>{f.icon}</span>
              <h3>{f.title}</h3>
              <p>{f.text}</p>
            </article>
          ))}
        </section>

        <section className="stack">
          <h2>Built with</h2>
          <ul className="chips">{stack.map((s) => <li key={s} className="chip">{s}</li>)}</ul>
        </section>
      </main>

      <footer className="footer">
        <span>Built by Tajveed Aslam</span>
        <a href={GITHUB_URL} target="_blank" rel="noreferrer">Source on GitHub</a>
      </footer>
    </div>
  )
}

function ServerStatus({ state }: { state: ReturnType<typeof useServerWake> }) {
  if (state === 'waking')
    return <p className="server-status"><span className="spinner" /> Waking up the demo server — free hosting sleeps when idle, this can take up to a minute.</p>
  if (state === 'down') return <p className="server-status error-text">The demo server isn&apos;t responding right now. Please try again later.</p>
  if (state === 'ready') return <p className="server-status ok-text">● Demo server online</p>
  return null
}
