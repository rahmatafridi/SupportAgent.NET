import { useEffect, useState } from 'react'
import { addAgentMessage, fetchTicket, fetchTicketMessages, updateTicketPriority, updateTicketStatus } from '../services/ticketService'
import type { Ticket, TicketMessage } from '../types/ticket'

interface TicketConversationProps { ticketId: number; canManage: boolean; refreshKey?: number; draftText?: string; onChanged?(): void }

export function TicketConversation({ ticketId, canManage, refreshKey = 0, draftText = '', onChanged }: TicketConversationProps) {
  const [ticket, setTicket] = useState<Ticket | null>(null)
  const [messages, setMessages] = useState<TicketMessage[]>([])
  const [reply, setReply] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  useEffect(() => { setError(null); Promise.all([fetchTicket(ticketId), fetchTicketMessages(ticketId)]).then(([item, items]) => { setTicket(item); setMessages(items) }).catch((err: Error) => setError(err.message)) }, [ticketId, refreshKey])
  useEffect(() => { if (draftText) setReply(draftText) }, [draftText])
  async function changeStatus(status: string) { try { setTicket(await updateTicketStatus(ticketId, status)); onChanged?.() } catch (err) { setError((err as Error).message) } }
  async function changePriority(priority: string) { try { setTicket(await updateTicketPriority(ticketId, priority)); onChanged?.() } catch (err) { setError((err as Error).message) } }
  async function sendReply() { if (!reply.trim()) { setError('Message is required.'); return } setBusy(true); setError(null); try { await addAgentMessage(ticketId, reply.trim()); setReply(''); setMessages(await fetchTicketMessages(ticketId)); onChanged?.() } catch (err) { setError((err as Error).message) } finally { setBusy(false) } }
  if (error && !ticket) return <div className="ticket-conversation panel-error">{error}</div>
  if (!ticket) return <div className="ticket-conversation">Loading ticket...</div>
  return <section className="ticket-conversation">
    <div className="panel-header"><h2>Ticket #{ticket.id}</h2><p className="ticket-summary">{ticket.subject} · Customer {ticket.customerId}</p><div className="ticket-controls"><label>Status<select value={ticket.status} disabled={!canManage} onChange={event => changeStatus(event.target.value)}><option>Open</option><option value="InProgress">In Progress</option><option>Closed</option></select></label><label>Priority<select value={ticket.priority} disabled={!canManage} onChange={event => changePriority(event.target.value)}><option>Low</option><option>Medium</option><option>High</option></select></label></div></div>
    {error && <p className="panel-error">{error}</p>}
    <div className="ticket-messages">{messages.map(message => <article key={message.id} className={message.senderType.toLowerCase() === 'customer' ? 'ticket-message customer' : 'ticket-message agent'}><header><strong>{message.senderType}</strong><span>{new Date(message.createdAt).toLocaleString()}</span></header><p>{message.message}</p></article>)}</div>
    {canManage && <div className="agent-reply"><label>Agent Reply<textarea rows={4} maxLength={4000} value={reply} onChange={event => setReply(event.target.value)} placeholder="Write a reply to the customer…" /></label><button onClick={sendReply} disabled={busy || !reply.trim()}>{busy ? 'Sending…' : 'Send Reply'}</button><p className="panel-help">AI drafts are not sent until you click Send Reply.</p></div>}
  </section>
}
