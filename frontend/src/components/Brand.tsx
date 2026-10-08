import { Link } from 'react-router-dom'

export default function Brand({ to = '/' }: { to?: string }) {
  return (
    <Link to={to} className="brand">
      <img src="/favicon.svg" alt="" width={26} height={26} />
      <span>
        Fit<strong>Check</strong>
      </span>
    </Link>
  )
}
