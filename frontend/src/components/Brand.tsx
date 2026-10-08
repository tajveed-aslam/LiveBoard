import { Link } from 'react-router-dom'

export function Brand({ to = '/', compact = false }: { to?: string; compact?: boolean }) {
  return (
    <Link to={to} className="brand" aria-label="LiveBoard home">
      <img src="/favicon.svg" alt="" width={26} height={26} />
      {!compact && (
        <span>
          Live<strong>Board</strong>
        </span>
      )}
    </Link>
  )
}

export default Brand
