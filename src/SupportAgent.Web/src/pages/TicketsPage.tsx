import { Link, useNavigate } from 'react-router-dom'
import { useState } from 'react'
import { TicketList } from '../components/TicketList'
import { AppHeader } from '../components/AppHeader'
import { NewTicketForm } from '../components/NewTicketForm'
import { useAuth } from '../auth/AuthContext'

export function TicketsPage() {
  const navigate = useNavigate(); const { user } = useAuth(); const [showNew, setShowNew] = useState(false); const [refreshKey, setRefreshKey] = useState(0)
  const canManage = user?.roles.some(role => role === 'Admin' || role === 'SupportAgent') === true
  return (
    <><AppHeader /><main className="workspace">
      <header className="workspace-header">
        <div>
          <h1>Support Workspace</h1>
          <p>Select a ticket to review the conversation and use the AI copilot.</p>
        </div>
        <div className="header-actions">{canManage && <button className="button-primary" onClick={() => setShowNew(true)}><span aria-hidden="true">＋</span> New ticket</button>}<Link to="/" className="workspace-link">Back to home</Link></div>
      </header>
      {showNew && <NewTicketForm onCancel={() => setShowNew(false)} onCreated={id => { setShowNew(false); setRefreshKey(value => value + 1); navigate(`/tickets/${id}`) }} />}
      <div className="workspace-grid single-column">
        <TicketList refreshKey={refreshKey} />
        <section className="empty-state">
          <p>Select a ticket from the list to open the support workspace.</p>
        </section>
      </div>
    </main></>
  )
}
