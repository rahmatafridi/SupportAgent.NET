let csrfToken: string | null = null

export function resetCsrfToken() { csrfToken = null }

async function getCsrfToken() {
  if (csrfToken) return csrfToken
  const response = await fetch('/api/auth/csrf', { credentials: 'include' })
  if (!response.ok) throw new Error('Could not initialize request security.')
  csrfToken = (await response.json() as { token: string }).token
  return csrfToken
}

export async function apiFetch(input: RequestInfo | URL, init: RequestInit = {}) {
  const method = (init.method ?? 'GET').toUpperCase()
  const headers = new Headers(init.headers)
  if (!['GET', 'HEAD', 'OPTIONS'].includes(method))
    headers.set('X-CSRF-TOKEN', await getCsrfToken())
  return fetch(input, { ...init, headers, credentials: 'include' })
}
