import type { Ticket, TicketListItem, TicketMessage } from '../types/ticket'

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

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(readApiErrorMessage(errorText, response.status))
  }

  return response.json() as Promise<T>
}

export async function fetchTickets(): Promise<TicketListItem[]> {
  const response = await fetch('/api/tickets')
  return readJson<TicketListItem[]>(response)
}

export async function fetchTicket(ticketId: number): Promise<Ticket> {
  const response = await fetch(`/api/tickets/${ticketId}`)
  return readJson<Ticket>(response)
}

export async function fetchTicketMessages(ticketId: number): Promise<TicketMessage[]> {
  const response = await fetch(`/api/tickets/${ticketId}/messages`)
  return readJson<TicketMessage[]>(response)
}
