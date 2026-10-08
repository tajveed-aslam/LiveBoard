import type { ReactNode } from 'react'
import { Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { useAuth } from './auth-context'
import BoardPage from './pages/BoardPage'
import BoardsPage from './pages/BoardsPage'
import JoinPage from './pages/JoinPage'
import Landing from './pages/Landing'
import Login from './pages/Login'

function RequireAuth({ children }: { children: ReactNode }) {
  const { session } = useAuth()
  const location = useLocation()
  return session ? children : <Navigate to="/login" replace state={{ from: location.pathname }} />
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Landing />} />
      <Route path="/login" element={<Login />} />
      <Route path="/join/:token" element={<JoinPage />} />
      <Route path="/boards" element={<RequireAuth><BoardsPage /></RequireAuth>} />
      <Route path="/b/:boardId" element={<RequireAuth><BoardPage /></RequireAuth>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
