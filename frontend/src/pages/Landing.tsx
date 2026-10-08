import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError } from '../api'
import { useAuth } from '../auth-context'
import Brand from '../components/Brand'
import ScoreGauge from '../components/ScoreGauge'
import { useServerWake } from '../components/useServerWake'

const GITHUB_URL = 'https://github.com/tajveed-aslam/FitCheck'

const features = [
  {
    icon: '🎯',
    title: 'Match score',
    text: 'A 0–100 score weighted toward the role’s required qualifications, with a plain-English summary of where you stand.',
  },
  {
    icon: '🔍',
    title: 'Skills & gaps',
    text: 'The skills you already show, and the keywords you’re missing, ranked required, preferred or minor and highlighted in the job post.',
  },
  {
    icon: '💡',
    title: 'Three concrete tips',
    text: 'Specific, honest advice on what to surface, quantify or reword in your CV for this role. Never “just add the keyword”.',
  },
]

const steps = [
  ['Upload', 'Your CV as a PDF or DOCX. Only the extracted text is kept, never the file.'],
  ['Paste', 'The job description you’re applying for.'],
  ['Review', 'Your score, gaps and tips. Every analysis is saved to your history.'],
]

const stack = ['ASP.NET Core 8', 'EF Core', 'PostgreSQL', 'JWT auth', 'PdfPig', 'Open XML SDK', 'React', 'TypeScript', 'Gemini API']

export default function Landing() {
  const { session, startGuest } = useAuth()
  const navigate = useNavigate()
  const server = useServerWake()
  const [starting, setStarting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function tryDemo() {
    if (session) return navigate('/app')
    setStarting(true)
    setError(null)
    try {
      await startGuest()
      navigate('/app')
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Could not start the demo.')
    } finally {
      setStarting(false)
    }
  }

  return (
    <div className="landing">
      <header className="topbar">
        <Brand />
        <nav className="topbar-actions">
          <a href={GITHUB_URL} target="_blank" rel="noreferrer" className="link-muted">GitHub</a>
          {session ? (
            <Link to="/app" className="btn btn-secondary">Open app</Link>
          ) : (
            <Link to="/login" className="btn btn-secondary">Sign in</Link>
          )}
        </nav>
      </header>

      <main>
        <section className="hero hero-split">
          <div className="hero-copy">
            <p className="eyebrow">AI-assisted CV review</p>
            <h1>
              See how well your CV fits the job, <span className="accent">before you apply</span>
            </h1>
            <p className="lead">
              Upload your CV and paste a job description. FitCheck scores the match, shows which skills line up and which
              keywords you’re missing, and gives you three specific ways to close the gap.
            </p>
            <div className="hero-actions">
              <button className="btn btn-primary btn-lg" onClick={tryDemo} disabled={starting}>
                {starting ? <><span className="spinner" /> Starting demo…</> : session ? 'Open the app' : 'Try the live demo'}
              </button>
              {!session && <Link to="/login" className="btn btn-ghost btn-lg">Create an account</Link>}
            </div>
            <p className="hint">No sign-up needed. A sample CV and job post are one click away inside the demo.</p>
            {error && <p className="error-text">{error}</p>}
            <ServerStatus state={server} />
          </div>

          <div className="hero-preview card" aria-hidden>
            <ScoreGauge score={78} />
            <div className="preview-tags">
              <span className="tag tag-matched">Playwright</span>
              <span className="tag tag-matched">TypeScript</span>
              <span className="tag tag-matched">REST APIs</span>
              <span className="tag tag-missing importance-high">Kubernetes<span className="tag-badge">Required</span></span>
              <span className="tag tag-missing importance-medium">k6<span className="tag-badge">Preferred</span></span>
            </div>
            <p className="preview-tip"><strong>Tip:</strong> lead with the regression-time win, and put a number on it.</p>
          </div>
        </section>

        <section className="cards">
          {features.map((f) => (
            <article key={f.title} className="card feature">
              <span className="feature-icon" aria-hidden>{f.icon}</span>
              <h3>{f.title}</h3>
              <p>{f.text}</p>
            </article>
          ))}
        </section>

        <section className="steps">
          <h2>How it works</h2>
          <ol>
            {steps.map(([title, text], i) => (
              <li key={title}>
                <span className="step-number">{i + 1}</span>
                <div>
                  <strong>{title}</strong>
                  <p>{text}</p>
                </div>
              </li>
            ))}
          </ol>
        </section>

        <section className="stack">
          <h2>Built with</h2>
          <ul className="chips">
            {stack.map((s) => <li key={s} className="chip">{s}</li>)}
          </ul>
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
    return (
      <p className="server-status">
        <span className="spinner" /> Waking up the demo server — free hosting sleeps when idle, this can take up to a
        minute.
      </p>
    )
  if (state === 'down')
    return <p className="server-status error-text">The demo server isn't responding right now. Please try again later.</p>
  if (state === 'ready') return <p className="server-status ok-text">● Demo server online</p>
  return null
}
