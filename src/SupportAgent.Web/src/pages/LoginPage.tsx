import { useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function LoginPage() {
  const { user, login } = useAuth(); const navigate = useNavigate()
  const [email, setEmail] = useState(''); const [password, setPassword] = useState(''); const [error, setError] = useState('')
  if (user) return <Navigate to="/tickets" replace />
  async function submit(event: FormEvent) { event.preventDefault(); setError(''); try { await login(email, password); navigate('/tickets') } catch (e) { setError((e as Error).message) } }
  return <main className="auth-page"><form className="auth-card" onSubmit={submit}><h1>Sign in</h1>
    <label>Email<input type="email" value={email} onChange={e => setEmail(e.target.value)} required /></label>
    <label>Password<input type="password" value={password} onChange={e => setPassword(e.target.value)} required /></label>
    {error && <p className="panel-error">{error}</p>}<button>Sign in</button><p><Link to="/register">Create an organization</Link></p>
  </form></main>
}
