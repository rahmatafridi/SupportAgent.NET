import type {
  CopilotAskRequest,
  CopilotAskResponse,
  CopilotDraftRequest,
  CopilotDraftResponse,
} from '../types/copilot'
import { apiFetch } from './api'

function readApiErrorMessage(errorText: string, status: number): string {
  if (!errorText) {
    return `Request failed with status ${status}`
  }

  try {
    const parsed = JSON.parse(errorText) as { error?: string }
    if (parsed.error) {
      return parsed.error
    }
  } catch {
    // Response body is plain text, not JSON.
  }

  return errorText
}

export async function askCopilot(request: CopilotAskRequest): Promise<CopilotAskResponse> {
  const response = await apiFetch('/api/copilot/ask', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(readApiErrorMessage(errorText, response.status))
  }

  return response.json() as Promise<CopilotAskResponse>
}

export async function draftCopilotReply(
  request: CopilotDraftRequest,
): Promise<CopilotDraftResponse> {
  const response = await apiFetch('/api/copilot/draft-reply', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(readApiErrorMessage(errorText, response.status))
  }

  return response.json() as Promise<CopilotDraftResponse>
}
