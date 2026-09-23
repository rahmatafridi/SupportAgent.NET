import { Link, useParams } from 'react-router-dom'
import { TicketList } from '../components/TicketList'
import { TicketConversation } from '../components/TicketConversation'
import { CopilotPanel } from '../components/CopilotPanel'

export function TicketDetailPage() {
  const { id } = useParams()
  const ticketId = Number(id)

  if (!Number.isFinite(ticketId) || ticketId <= 0) {
    return (
      <main className="workspace">
        <p className="panel-error">Invalid ticket ID.</p>
        <Link to="/tickets">Back to tickets</Link>
      </main>
    )
  }

  return (
    <main className="workspace">
      <header className="workspace-header">
        <div>
          <h1>Support Workspace</h1>
          <p>Ticket conversation on the left, AI copilot on the right.</p>
        </div>
        <Link to="/" className="workspace-link">Back to home</Link>
      </header>
      <div className="workspace-grid">
        <TicketList />
        <TicketConversation ticketId={ticketId} />
        <CopilotPanel ticketId={ticketId} />
      </div>
    </main>
  )
}
