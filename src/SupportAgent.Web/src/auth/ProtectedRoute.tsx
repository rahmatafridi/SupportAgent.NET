import { Navigate } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from './AuthContext'

export function ProtectedRoute({ children, role }: { children: ReactNode; role?: string }) {
  const { user, loading } = useAuth()
  if (loading) return <p className="loading">Loading…</p>
  if (!user) return <Navigate to="/login" replace />
  if (role && !user.roles.includes(role)) return <Navigate to="/tickets" replace />
  return children
}
