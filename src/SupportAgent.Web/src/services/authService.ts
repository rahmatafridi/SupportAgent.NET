import { apiFetch, resetCsrfToken } from './api'

export type CurrentUser = {
  id: string
  email: string
  displayName: string
  organizationId: string
  organizationName: string
  roles: string[]
}

export async function getCurrentUser(): Promise<CurrentUser | null> {
  const response = await apiFetch('/api/auth/me')
  if (response.status === 401) return null
  if (!response.ok) throw new Error('Could not load the current user.')
  return response.json()
}

export async function login(email: string, password: string) {
  const response = await apiFetch('/api/auth/login', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password }),
  })
  if (!response.ok) throw new Error('Invalid email or password.')
  resetCsrfToken()
}

export async function register(organizationName: string, email: string, password: string) {
  const response = await apiFetch('/api/auth/register', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ organizationName, email, password }),
  })
  if (!response.ok) {
    const body = await response.json().catch(() => ({})) as { error?: string; errors?: string[] }
    throw new Error(body.error ?? body.errors?.join(' ') ?? 'Registration failed.')
  }
}

export async function logout() {
  const response = await apiFetch('/api/auth/logout', { method: 'POST' })
  if (!response.ok) throw new Error('Logout failed.')
  resetCsrfToken()
}
