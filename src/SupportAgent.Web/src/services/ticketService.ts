import type { Ticket, TicketListItem, TicketMessage } from '../types/ticket'
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

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(readApiErrorMessage(errorText, response.status))
  }

  return response.json() as Promise<T>
}

export async function fetchTickets(): Promise<TicketListItem[]> {
  const response = await apiFetch('/api/tickets')
  return readJson<TicketListItem[]>(response)
}

export async function fetchTicket(ticketId: number): Promise<Ticket> {
  const response = await apiFetch(`/api/tickets/${ticketId}`)
  return readJson<Ticket>(response)
}

export async function fetchTicketMessages(ticketId: number): Promise<TicketMessage[]> {
  const response = await apiFetch(`/api/tickets/${ticketId}/messages`)
  return readJson<TicketMessage[]>(response)
}

export async function createTicket(request: { customerId: number; subject: string; priority: string; message: string }): Promise<Ticket> {
  return readJson<Ticket>(await apiFetch('/api/tickets', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request) }))
}

export async function updateTicketStatus(ticketId: number, status: string): Promise<Ticket> {
  return readJson<Ticket>(await apiFetch(`/api/tickets/${ticketId}/status`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status }) }))
}

export async function updateTicketPriority(ticketId: number, priority: string): Promise<Ticket> {
  return readJson<Ticket>(await apiFetch(`/api/tickets/${ticketId}/priority`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ priority }) }))
}

export async function addAgentMessage(ticketId: number, message: string): Promise<TicketMessage> {
  return readJson<TicketMessage>(await apiFetch(`/api/tickets/${ticketId}/messages`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ message }) }))
}
