import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function AppHeader() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  if (!user) return null
  return <header className="app-header">
    <div><strong>{user.displayName}</strong> · {user.organizationName} · {user.roles.join(', ')}</div>
    <nav><Link to="/tickets">Tickets</Link>{user.roles.some(role => role === 'Admin' || role === 'SupportAgent') && <Link to="/knowledge">Knowledge</Link>}{user.roles.includes('Admin') && <Link to="/admin/usage">AI usage</Link>}
      <button onClick={async () => { await logout(); navigate('/login') }}>Log out</button></nav>
  </header>
}
