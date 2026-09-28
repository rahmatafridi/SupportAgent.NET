let csrfToken: string | null = null

export function resetCsrfToken() { csrfToken = null }

async function getCsrfToken() {
  if (csrfToken) return csrfToken
  const response = await fetch('/api/auth/csrf', { credentials: 'include', cache: 'no-store' })
  if (!response.ok) throw new Error('Your session security token could not be refreshed. Please reload the page and try again.')
  const body = await response.json() as { token?: string }
  if (!body.token) throw new Error('Your session security token could not be refreshed. Please reload the page and try again.')
  csrfToken = body.token
  return csrfToken
}

export async function apiFetch(input: RequestInfo | URL, init: RequestInit = {}) {
  const method = (init.method ?? 'GET').toUpperCase()
  const protectedMutation = !['GET', 'HEAD', 'OPTIONS'].includes(method)
  const send = async () => {
    const headers = new Headers(init.headers)
    if (protectedMutation) headers.set('X-CSRF-TOKEN', await getCsrfToken())
    return fetch(input, { ...init, headers, credentials: 'include' })
  }
  let response = await send()
  if (protectedMutation && response.status === 400) {
    const body = await response.clone().json().catch(() => null) as { error?: string } | null
    if (body?.error === 'A valid CSRF token is required.') {
      resetCsrfToken()
      response = await send()
    }
  }
  return response
}
