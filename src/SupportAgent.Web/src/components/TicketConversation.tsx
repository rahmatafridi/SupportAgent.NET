import { useEffect, useRef, useState } from 'react'
import { addAgentMessage, addInternalNote, assignTicketToMe, fetchAssignees, fetchCustomerContext, fetchInternalNotes, fetchTicket, fetchTicketMessages, updateTicketAssignee, updateTicketPriority, updateTicketStatus } from '../services/ticketService'
import type { Ticket, TicketAssignee, TicketCustomerContext, TicketInternalNote, TicketMessage } from '../types/ticket'

interface Props { ticketId: number; canManage: boolean; refreshKey?: number; reply: string; onReplyChange(value: string): void; focusReplyKey?: number; onChanged?(): void }

export function TicketConversation({ ticketId, canManage, refreshKey = 0, reply, onReplyChange, focusReplyKey = 0, onChanged }: Props) {
  const [ticket, setTicket] = useState<Ticket | null>(null); const [messages, setMessages] = useState<TicketMessage[]>([])
  const [context, setContext] = useState<TicketCustomerContext | null>(null); const [assignees, setAssignees] = useState<TicketAssignee[]>([])
  const [notes, setNotes] = useState<TicketInternalNote[]>([]); const [tab, setTab] = useState<'conversation' | 'notes'>('conversation')
  const [note, setNote] = useState(''); const [error, setError] = useState<string | null>(null); const [busy, setBusy] = useState(false)
  const replyRef = useRef<HTMLTextAreaElement | null>(null)
  useEffect(() => {
    setError(null); setTicket(null); setTab('conversation')
    const requests: Promise<unknown>[] = [fetchTicket(ticketId).then(setTicket), fetchTicketMessages(ticketId).then(setMessages), fetchCustomerContext(ticketId).then(setContext), fetchAssignees().then(setAssignees)]
    if (canManage) requests.push(fetchInternalNotes(ticketId).then(setNotes))
    Promise.all(requests).catch((e: Error) => setError(e.message))
  }, [ticketId, refreshKey, canManage])
  useEffect(() => {
    if (focusReplyKey > 0) {
      setTab('conversation')
      requestAnimationFrame(() => {
        replyRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' })
        replyRef.current?.focus()
      })
    }
  }, [focusReplyKey])
  async function perform(action: () => Promise<Ticket>) { try { setError(null); setTicket(await action()); onChanged?.() } catch (e) { setError((e as Error).message) } }
  async function sendReply() { if (!reply.trim()) return; setBusy(true); try { await addAgentMessage(ticketId, reply.trim()); onReplyChange(''); setMessages(await fetchTicketMessages(ticketId)); onChanged?.() } catch (e) { setError((e as Error).message) } finally { setBusy(false) } }
  async function saveNote() { if (!note.trim()) return; setBusy(true); try { await addInternalNote(ticketId, note.trim()); setNote(''); setNotes(await fetchInternalNotes(ticketId)); onChanged?.() } catch (e) { setError((e as Error).message) } finally { setBusy(false) } }
  if (error && !ticket) return <section className="ticket-conversation"><p className="panel-error">{error}</p></section>
  if (!ticket) return <section className="ticket-conversation"><p className="empty-copy">Loading ticket details…</p></section>
  return <section className="ticket-conversation">
    <div className="panel-header"><h2>Ticket #{ticket.id}</h2><p className="ticket-summary">{ticket.subject} · Customer {ticket.customerId}</p>
      <div className="ticket-times"><span>Created {new Date(ticket.createdAt).toLocaleString()}</span><span>Last updated {new Date(ticket.updatedAt).toLocaleString()}</span></div>
      <div className="ticket-controls"><label>Status<select value={ticket.status} disabled={!canManage} onChange={e => perform(() => updateTicketStatus(ticketId, e.target.value))}><option>Open</option><option value="InProgress">In Progress</option><option>Closed</option></select></label><label>Priority<select value={ticket.priority} disabled={!canManage} onChange={e => perform(() => updateTicketPriority(ticketId, e.target.value))}><option>Low</option><option>Medium</option><option>High</option></select></label>
        <label>Assigned To<select value={ticket.assignedToUserId ?? ''} disabled={!canManage} onChange={e => perform(() => updateTicketAssignee(ticketId, e.target.value || null))}><option value="">Unassigned</option>{assignees.map(a => <option key={a.id} value={a.id}>{a.displayName}</option>)}</select></label>
        {canManage && <button type="button" className="secondary-button" onClick={() => perform(() => assignTicketToMe(ticketId))}>Assign to me</button>}
      </div>
    </div>
    {error && <p className="panel-error">{error}</p>}
    {context && <div className="customer-context"><div><h3>Customer</h3><strong>{context.name}</strong><span>{context.email}</span><span>{context.phone ?? 'No phone'}</span><span>Customer #{context.id}</span></div><div><h3>Recent orders</h3>{context.recentOrders.length === 0 ? <span>No recent orders.</span> : context.recentOrders.map(order => <div className="context-row" key={order.id}><strong>{order.orderNumber}</strong><span>{order.status} · ${order.totalAmount.toFixed(2)} · {new Date(order.createdAt).toLocaleDateString()}</span></div>)}</div><div><h3>Recent tickets</h3>{context.recentTickets.length === 0 ? <span>No recent tickets.</span> : context.recentTickets.map(item => <div className="context-row" key={item.id}><strong>#{item.id} {item.subject}</strong><span>{item.status} · {item.priority}</span></div>)}</div></div>}
    {canManage && <div className="detail-tabs"><button className={tab === 'conversation' ? 'active' : ''} onClick={() => setTab('conversation')}>Conversation</button><button className={tab === 'notes' ? 'active' : ''} onClick={() => setTab('notes')}>Internal Notes</button></div>}
    {tab === 'conversation' ? <><div className="ticket-messages">{messages.length === 0 ? <p className="empty-copy">No conversation messages.</p> : messages.map(message => <article key={message.id} className={`ticket-message ${message.senderType.toLowerCase() === 'customer' ? 'customer' : 'agent'}`}><header><strong>{message.senderType}</strong><span>{new Date(message.createdAt).toLocaleString()}</span></header><p>{message.message}</p></article>)}</div>
      {canManage && <div className="agent-reply"><label>Agent Reply<textarea ref={replyRef} rows={4} maxLength={4000} value={reply} onChange={e => onReplyChange(e.target.value)} placeholder="Write a reply to the customer…" /></label><button onClick={sendReply} disabled={busy || !reply.trim()}>{busy ? 'Sending…' : 'Send Reply'}</button><p className="panel-help">AI drafts are not sent until you click Send Reply.</p></div>}</> :
      <div className="internal-notes">{notes.length === 0 ? <p className="empty-copy">No internal notes.</p> : notes.map(item => <article key={item.id}><header><strong>Internal note</strong><span>{new Date(item.createdAt).toLocaleString()}</span></header><p>{item.content}</p></article>)}<label>Add internal note<textarea rows={3} maxLength={4000} value={note} onChange={e => setNote(e.target.value)} placeholder="Visible only to support staff…" /></label><button onClick={saveNote} disabled={busy || !note.trim()}>Add Note</button></div>}
  </section>
}
