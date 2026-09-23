import { Link, useParams } from 'react-router-dom'
import { useEffect, useState } from 'react'
import { fetchTickets } from '../services/ticketService'
import type { TicketListItem } from '../types/ticket'

export function TicketList() {
  const { id } = useParams()
  const selectedId = id ? Number(id) : null
  const [tickets, setTickets] = useState<TicketListItem[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    fetchTickets()
      .then(setTickets)
      .catch((err: Error) => setError(err.message))
  }, [])

  return (
    <aside className="ticket-list">
      <div className="panel-header">
        <h2>Tickets</h2>
      </div>
      {error && <p className="panel-error">{error}</p>}
      <ul>
        {tickets.map((ticket) => (
          <li key={ticket.id}>
            <Link
              to={`/tickets/${ticket.id}`}
              className={selectedId === ticket.id ? 'ticket-link active' : 'ticket-link'}
            >
              <strong>#{ticket.id}</strong> {ticket.subject}
              <span className="ticket-meta">
                {ticket.priority} · {ticket.status} · {ticket.customerName}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </aside>
  )
}
