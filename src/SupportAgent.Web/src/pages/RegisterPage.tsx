import { useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function RegisterPage() {
  const { user, register } = useAuth(); const navigate = useNavigate()
  const [organization, setOrganization] = useState(''); const [email, setEmail] = useState(''); const [password, setPassword] = useState(''); const [error, setError] = useState('')
  if (user) return <Navigate to="/tickets" replace />
  async function submit(event: FormEvent) { event.preventDefault(); setError(''); try { await register(organization, email, password); navigate('/tickets') } catch (e) { setError((e as Error).message) } }
  return <main className="auth-page"><form className="auth-card" onSubmit={submit}><h1>Create organization</h1>
    <label>Organization<input value={organization} onChange={e => setOrganization(e.target.value)} required /></label>
    <label>Email<input type="email" value={email} onChange={e => setEmail(e.target.value)} required /></label>
    <label>Password<input type="password" value={password} onChange={e => setPassword(e.target.value)} minLength={8} required /></label>
    {error && <p className="panel-error">{error}</p>}<button>Register</button><p><Link to="/login">Back to sign in</Link></p>
  </form></main>
}
