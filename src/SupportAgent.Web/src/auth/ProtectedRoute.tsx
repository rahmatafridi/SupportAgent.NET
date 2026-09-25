import { Navigate } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from './AuthContext'

export function ProtectedRoute({ children, role, roles }: { children: ReactNode; role?: string; roles?: string[] }) {
  const { user, loading } = useAuth()
  if (loading) return <p className="loading">Loading…</p>
  if (!user) return <Navigate to="/login" replace />
  const landing = user.roles.includes('Customer') ? '/portal' : '/tickets'
  if (role && !user.roles.includes(role)) return <Navigate to={landing} replace />
  if (roles && !roles.some(item => user.roles.includes(item))) return <Navigate to={landing} replace />
  return children
}
