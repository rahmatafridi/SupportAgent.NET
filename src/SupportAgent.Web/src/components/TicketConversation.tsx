import { useEffect, useState } from 'react'
import { fetchTicket, fetchTicketMessages } from '../services/ticketService'
import type { Ticket, TicketMessage } from '../types/ticket'

interface TicketConversationProps {
  ticketId: number
}

export function TicketConversation({ ticketId }: TicketConversationProps) {
  const [ticket, setTicket] = useState<Ticket | null>(null)
  const [messages, setMessages] = useState<TicketMessage[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setError(null)
    Promise.all([fetchTicket(ticketId), fetchTicketMessages(ticketId)])
      .then(([ticketResult, messageResult]) => {
        setTicket(ticketResult)
        setMessages(messageResult)
      })
      .catch((err: Error) => setError(err.message))
  }, [ticketId])

  if (error) {
    return <div className="ticket-conversation panel-error">{error}</div>
  }

  if (!ticket) {
    return <div className="ticket-conversation">Loading ticket...</div>
  }

  return (
    <section className="ticket-conversation">
      <div className="panel-header">
        <h2>Ticket #{ticket.id}</h2>
        <p className="ticket-summary">
          {ticket.subject} · {ticket.priority} · {ticket.status} · Customer {ticket.customerId}
        </p>
      </div>
      <div className="ticket-messages">
        {messages.map((message) => (
          <article
            key={message.id}
            className={
              message.senderType.toLowerCase() === 'customer'
                ? 'ticket-message customer'
                : 'ticket-message agent'
            }
          >
            <header>
              <strong>{message.senderType}</strong>
              <span>{new Date(message.createdAt).toLocaleString()}</span>
            </header>
            <p>{message.message}</p>
          </article>
        ))}
      </div>
    </section>
  )
}
