import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { AppHeader } from '../components/AppHeader'
import { addPortalMessage, fetchPortalMessages, fetchPortalTicket, markPortalTicketRead } from '../services/portalService'
import type { PortalMessage, PortalTicket } from '../types/portal'

export function PortalTicketPage() {
  const id = Number(useParams().id); const [ticket, setTicket] = useState<PortalTicket | null>(null); const [messages, setMessages] = useState<PortalMessage[]>([]); const [reply, setReply] = useState(''); const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  async function load() { const [item, conversation] = await Promise.all([fetchPortalTicket(id), fetchPortalMessages(id)]); setTicket(item); setMessages(conversation); if (item.hasUnreadAgentReply) await markPortalTicketRead(id) }
  useEffect(() => { load().catch(e => setError((e as Error).message)) }, [id])
  async function send(event: React.FormEvent) { event.preventDefault(); if (!reply.trim()) return; setBusy(true); try { await addPortalMessage(id, reply.trim()); setReply(''); await load() } catch (e) { setError((e as Error).message) } finally { setBusy(false) } }
  return <><AppHeader /><main className="portal-page"><Link to="/portal">← My support requests</Link>{error && <p className="panel-error">{error}</p>}{!ticket ? <p className="empty-copy">Loading request…</p> : <section className="portal-detail"><header><h1>Ticket #{ticket.id}</h1><h2>{ticket.subject}</h2><p>{ticket.status === 'InProgress' ? 'In Progress' : ticket.status} · Created {new Date(ticket.createdAt).toLocaleString()} · Updated {new Date(ticket.updatedAt).toLocaleString()}</p></header><div className="ticket-messages">{messages.map(item => <article key={item.id} className={`ticket-message ${item.senderType.toLowerCase() === 'customer' ? 'customer' : 'agent'}`}><header><strong>{item.senderType === 'Customer' ? 'You' : 'Support Agent'}</strong><span>{new Date(item.createdAt).toLocaleString()}</span></header><p>{item.message}</p></article>)}</div><form className="portal-reply" onSubmit={send}><label>Write a reply<textarea rows={4} maxLength={4000} value={reply} onChange={e => setReply(e.target.value)} placeholder="Write a reply…" /></label><button disabled={busy || !reply.trim()}>{busy ? 'Sending…' : 'Send Reply'}</button></form></section>}</main></>
}
