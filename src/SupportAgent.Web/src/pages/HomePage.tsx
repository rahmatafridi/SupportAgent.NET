import { Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function HomePage() {
  const { user, loading } = useAuth()
  if (loading) return <p className="loading">Loading…</p>
  if (!user) return <Navigate to="/login" replace />
  return <Navigate to={user.roles.includes('Customer') ? '/portal' : '/tickets'} replace />
}
