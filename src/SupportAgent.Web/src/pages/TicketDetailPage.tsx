import { Link, useParams } from 'react-router-dom'
import { TicketList } from '../components/TicketList'
import { TicketConversation } from '../components/TicketConversation'
import { CopilotPanel } from '../components/CopilotPanel'
import { AppHeader } from '../components/AppHeader'
import { useAuth } from '../auth/AuthContext'
import { useState } from 'react'

export function TicketDetailPage() {
  const { id } = useParams()
  const ticketId = Number(id)
  const { user } = useAuth()
  const canUseAi = user?.roles.some(role => role === 'Admin' || role === 'SupportAgent') === true
  const [refreshKey, setRefreshKey] = useState(0)
  const [draftText, setDraftText] = useState('')

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
        <TicketList refreshKey={refreshKey} />
        <TicketConversation ticketId={ticketId} canManage={canUseAi} refreshKey={refreshKey} draftText={draftText} onChanged={() => setRefreshKey(value => value + 1)} />
        {canUseAi && <CopilotPanel ticketId={ticketId} onUseDraft={text => setDraftText(text)} />}
      </div>
    </main></>
  )
}
