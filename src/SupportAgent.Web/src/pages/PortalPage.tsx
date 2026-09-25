import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { AppHeader } from '../components/AppHeader'
import { createPortalTicket, fetchPortalOrders, fetchPortalProfile, fetchPortalTickets } from '../services/portalService'
import type { PortalOrder, PortalProfile, PortalTicketList } from '../types/portal'

export function PortalPage() {
  const navigate = useNavigate()
  const [profile, setProfile] = useState<PortalProfile | null>(null)
  const [data, setData] = useState<PortalTicketList | null>(null)
  const [orders, setOrders] = useState<PortalOrder[]>([])
  const [showForm, setShowForm] = useState(false)
  const [subject, setSubject] = useState('')
  const [priority, setPriority] = useState('Medium')
  const [message, setMessage] = useState('')
  const [orderId, setOrderId] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    Promise.all([
      fetchPortalProfile().then(setProfile),
      fetchPortalTickets().then(setData),
      fetchPortalOrders().then(setOrders),
    ]).catch(e => setError((e as Error).message))
  }, [])

  async function create(event: React.FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      const ticket = await createPortalTicket({ subject, priority, message, orderId: orderId ? Number(orderId) : null })
      navigate(`/portal/tickets/${ticket.id}`)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return <><AppHeader /><main className="portal-page">
    <header className="portal-hero"><div><h1>My Support Requests</h1>{profile && <p>{profile.name} · {profile.email} · {profile.organizationName}</p>}</div><button className="button-primary" onClick={() => setShowForm(value => !value)}>+ Create Support Request</button></header>
    {error && <p className="panel-error">{error}</p>}
    {data && <div className="portal-summary"><span><strong>{data.summary.open}</strong>Open</span><span><strong>{data.summary.inProgress}</strong>In Progress</span><span><strong>{data.summary.closed}</strong>Closed</span></div>}
    {showForm && <form className="portal-form" onSubmit={create}><h2>New support request</h2><label>Subject<input value={subject} maxLength={200} onChange={e => setSubject(e.target.value)} required /></label><label>Priority<select value={priority} onChange={e => setPriority(e.target.value)}><option>Low</option><option>Medium</option><option>High</option></select></label><label>Related order<select value={orderId} onChange={e => setOrderId(e.target.value)}><option value="">None</option>{orders.map(order => <option key={order.id} value={order.id}>{order.orderNumber}</option>)}</select></label><label>Message<textarea value={message} maxLength={4000} rows={5} onChange={e => setMessage(e.target.value)} required /></label><button disabled={busy}>{busy ? 'Creating…' : 'Create Request'}</button></form>}
    {!data ? <p className="empty-copy">Loading support requests…</p> : data.tickets.length === 0 ? <p className="empty-state portal-empty">No support requests yet.</p> : <div className="portal-ticket-list">{data.tickets.map(ticket => <Link className={ticket.hasUnreadAgentReply ? 'unread' : ''} key={ticket.id} to={`/portal/tickets/${ticket.id}`}><strong>#{ticket.id} {ticket.subject}{ticket.hasUnreadAgentReply && <span className="unread-badge">New reply</span>}</strong><span>{ticket.status === 'InProgress' ? 'In Progress' : ticket.status}</span><small>Created {new Date(ticket.createdAt).toLocaleDateString()} · Updated {new Date(ticket.updatedAt).toLocaleString()}</small></Link>)}</div>}
  </main></>
}
