import { Link, useParams } from 'react-router-dom'
import { useEffect, useState } from 'react'
import { fetchTickets, fetchTicketSummary } from '../services/ticketService'
import type { TicketFilters, TicketListItem, TicketSummary } from '../types/ticket'

const defaults: TicketFilters = { search: '', status: 'All', priority: 'All', sort: 'updated', assignedToMe: false }

export function TicketList({ refreshKey = 0 }: { refreshKey?: number }) {
  const { id } = useParams(); const selectedId = id ? Number(id) : null
  const [tickets, setTickets] = useState<TicketListItem[]>([])
  const [summary, setSummary] = useState<TicketSummary | null>(null)
  const [filters, setFilters] = useState(defaults)
  const [error, setError] = useState<string | null>(null)
  const [summaryError, setSummaryError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  useEffect(() => { const timer = window.setTimeout(() => { setLoading(true); setError(null); fetchTickets(filters).then(setTickets).catch((e: Error) => setError(e.message)).finally(() => setLoading(false)) }, 250); return () => window.clearTimeout(timer) }, [filters, refreshKey])
  useEffect(() => { fetchTicketSummary().then(setSummary).catch((e: Error) => setSummaryError(e.message)) }, [refreshKey])
  function setFilter<K extends keyof TicketFilters>(key: K, value: TicketFilters[K]) { setFilters(current => ({ ...current, [key]: value })) }
  return <aside className="ticket-list">
    <div className="panel-header"><h2>Tickets</h2></div>
    {summary && <div className="summary-cards"><span><strong>{summary.open}</strong> Open</span><span><strong>{summary.inProgress}</strong> In progress</span><span><strong>{summary.highPriority}</strong> High</span><span><strong>{summary.unassigned}</strong> Unassigned</span><span><strong>{summary.closedToday}</strong> Closed today</span></div>}
    {summaryError && <p className="panel-error compact">Summary unavailable: {summaryError}</p>}
    <div className="ticket-filters">
      <input aria-label="Search tickets" placeholder="Search tickets…" value={filters.search} onChange={e => setFilter('search', e.target.value)} />
      <div className="filter-row"><select aria-label="Filter status" value={filters.status} onChange={e => setFilter('status', e.target.value)}><option>All</option><option>Open</option><option value="InProgress">In Progress</option><option>Closed</option></select><select aria-label="Filter priority" value={filters.priority} onChange={e => setFilter('priority', e.target.value)}><option>All</option><option>Low</option><option>Medium</option><option>High</option></select></div>
      <select aria-label="Sort tickets" value={filters.sort} onChange={e => setFilter('sort', e.target.value)}><option value="updated">Recently updated</option><option value="newest">Newest first</option><option value="oldest">Oldest first</option><option value="priority">Highest priority</option></select>
      <label className="checkbox-label"><input type="checkbox" checked={filters.assignedToMe} onChange={e => setFilter('assignedToMe', e.target.checked)} /> Assigned to me</label>
    </div>
    {error && <p className="panel-error">{error}</p>}
    {loading ? <p className="empty-copy">Loading tickets…</p> : tickets.length === 0 ? <p className="empty-copy">No tickets found.</p> : <ul>{tickets.map(ticket => <li key={ticket.id}><Link to={`/tickets/${ticket.id}`} className={selectedId === ticket.id ? 'ticket-link active' : 'ticket-link'}>
      <div className="ticket-title"><strong>#{ticket.id}</strong><span>{ticket.subject}</span></div><span className="ticket-customer">{ticket.customerName}</span>
      <div className="ticket-badges"><span className={`badge status-${ticket.status.toLowerCase()}`}>{ticket.status === 'InProgress' ? 'In Progress' : ticket.status}</span><span className={`badge priority-${ticket.priority.toLowerCase()}`}>{ticket.priority}</span></div>
      <span className="ticket-meta">{ticket.assignedToName ?? 'Unassigned'} · Updated {new Date(ticket.updatedAt).toLocaleString()}</span>
    </Link></li>)}</ul>}
  </aside>
}
