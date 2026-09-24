import { Link, useParams } from 'react-router-dom'
import { TicketList } from '../components/TicketList'
import { TicketConversation } from '../components/TicketConversation'
import { CopilotPanel } from '../components/CopilotPanel'
import { AppHeader } from '../components/AppHeader'
import { useAuth } from '../auth/AuthContext'

export function TicketDetailPage() {
  const { id } = useParams()
  const ticketId = Number(id)
  const { user } = useAuth()
  const canUseAi = user?.roles.some(role => role === 'Admin' || role === 'SupportAgent') === true

  if (!Number.isFinite(ticketId) || ticketId <= 0) {
    return (
      <main className="workspace">
        <p className="panel-error">Invalid ticket ID.</p>
        <Link to="/tickets">Back to tickets</Link>
      </main>
    )
  }

  return (
    <><AppHeader /><main className="workspace">
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
        {canUseAi && <CopilotPanel ticketId={ticketId} />}
      </div>
    </main></>
  )
}
