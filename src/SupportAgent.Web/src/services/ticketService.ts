import type { Ticket, TicketAssignee, TicketCustomerContext, TicketFilters, TicketInternalNote, TicketListItem, TicketMessage, TicketSummary } from '../types/ticket'
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

  return status >= 500 ? 'The server could not load support data. Please try again.' : errorText
}

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(readApiErrorMessage(errorText, response.status))
  }

  return response.json() as Promise<T>
}

export async function fetchTickets(filters?: Partial<TicketFilters>): Promise<TicketListItem[]> {
  const params = new URLSearchParams()
  if (filters?.search) params.set('search', filters.search)
  if (filters?.status && filters.status !== 'All') params.set('status', filters.status)
  if (filters?.priority && filters.priority !== 'All') params.set('priority', filters.priority)
  if (filters?.sort) params.set('sort', filters.sort)
  if (filters?.assignedToMe) params.set('assignedToMe', 'true')
  const response = await apiFetch(`/api/tickets${params.size ? `?${params}` : ''}`)
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

export async function fetchTicketSummary(): Promise<TicketSummary> { return readJson<TicketSummary>(await apiFetch('/api/tickets/summary')) }
export async function fetchAssignees(): Promise<TicketAssignee[]> { return readJson<TicketAssignee[]>(await apiFetch('/api/tickets/assignees')) }
export async function updateTicketAssignee(ticketId: number, assignedToUserId: string | null): Promise<Ticket> { return readJson<Ticket>(await apiFetch(`/api/tickets/${ticketId}/assignee`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ assignedToUserId }) })) }
export async function assignTicketToMe(ticketId: number): Promise<Ticket> { return readJson<Ticket>(await apiFetch(`/api/tickets/${ticketId}/assign-to-me`, { method: 'PATCH' })) }
export async function fetchInternalNotes(ticketId: number): Promise<TicketInternalNote[]> { return readJson<TicketInternalNote[]>(await apiFetch(`/api/tickets/${ticketId}/notes`)) }
export async function addInternalNote(ticketId: number, content: string): Promise<TicketInternalNote> { return readJson<TicketInternalNote>(await apiFetch(`/api/tickets/${ticketId}/notes`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ content }) })) }
export async function fetchCustomerContext(ticketId: number): Promise<TicketCustomerContext> { return readJson<TicketCustomerContext>(await apiFetch(`/api/tickets/${ticketId}/customer-context`)) }

export async function createTicket(request: { customerId: number; subject: string; priority: string; message: string }): Promise<Ticket> {
  return readJson<Ticket>(await apiFetch('/api/tickets', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request) }))
}

export async function updateTicketStatus(ticketId: number, status: string): Promise<Ticket> {
  return readJson<Ticket>(await apiFetch(`/api/tickets/${ticketId}/status`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status }) }))
}

export async function updateTicketPriority(ticketId: number, priority: string): Promise<Ticket> {
  return readJson<Ticket>(await apiFetch(`/api/tickets/${ticketId}/priority`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ priority }) }))
}

export async function updateTicketOrder(ticketId: number, orderId: number | null): Promise<Ticket> {
  return readJson<Ticket>(await apiFetch(`/api/tickets/${ticketId}/order`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ orderId }) }))
}

export async function addAgentMessage(ticketId: number, message: string): Promise<TicketMessage> {
  return readJson<TicketMessage>(await apiFetch(`/api/tickets/${ticketId}/messages`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ message }) }))
}
