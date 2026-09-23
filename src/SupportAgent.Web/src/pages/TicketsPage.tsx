import { Link } from 'react-router-dom'
import { TicketList } from '../components/TicketList'

export function TicketsPage() {
  return (
    <main className="workspace">
      <header className="workspace-header">
        <div>
          <h1>Support Workspace</h1>
          <p>Select a ticket to review the conversation and use the AI copilot.</p>
        </div>
        <Link to="/" className="workspace-link">Back to home</Link>
      </header>
      <div className="workspace-grid single-column">
        <TicketList />
        <section className="empty-state">
          <p>Select a ticket from the list to open the support workspace.</p>
        </section>
      </div>
    </main>
  )
}
