import { apiFetch } from './api'
import type { PortalMessage, PortalOrder, PortalProfile, PortalTicket, PortalTicketList } from '../types/portal'
async function read<T>(response: Response): Promise<T> { if (!response.ok) throw new Error(response.status === 404 ? 'Support request not found.' : 'Portal request failed.'); return response.json() as Promise<T> }
export const fetchPortalProfile = () => apiFetch('/api/portal/me').then(read<PortalProfile>)
export const fetchPortalTickets = () => apiFetch('/api/portal/tickets').then(read<PortalTicketList>)
export const fetchPortalTicket = (id: number) => apiFetch(`/api/portal/tickets/${id}`).then(read<PortalTicket>)
export const fetchPortalMessages = (id: number) => apiFetch(`/api/portal/tickets/${id}/messages`).then(read<PortalMessage[]>)
export const markPortalTicketRead = async (id: number) => { const response = await apiFetch(`/api/portal/tickets/${id}/read`, { method: 'POST' }); if (!response.ok) throw new Error('Unable to mark the support request as read.') }
export const fetchPortalOrders = () => apiFetch('/api/portal/orders').then(read<PortalOrder[]>)
export const createPortalTicket = (body: { subject: string; priority: string; message: string; orderId: number | null }) => apiFetch('/api/portal/tickets', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }).then(read<PortalTicket>)
export const addPortalMessage = (id: number, message: string) => apiFetch(`/api/portal/tickets/${id}/messages`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ message }) }).then(read<PortalMessage>)
