import type { ChatRequest, ChatResponse } from '../types/chat'

function readApiErrorMessage(errorText: string, status: number): string {
  if (!errorText) {
    return `Chat request failed with status ${status}`
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

export async function sendChatMessage(message: string): Promise<ChatResponse> {
  const response = await fetch('/api/ai/chat', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ message } satisfies ChatRequest),
  })

  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(readApiErrorMessage(errorText, response.status))
  }

  return response.json() as Promise<ChatResponse>
}
