import { Link } from 'react-router-dom'
import { BackendStatus } from '../components/BackendStatus'
import { ChatTestPanel } from '../components/ChatTestPanel'

export function HomePage() {
  return (
    <main className="home">
      <h1>SupportAgent.NET</h1>
      <p className="tagline">Open-source AI Customer Support Agent Starter Kit</p>
      <BackendStatus />
      <p className="home-links">
        <Link to="/tickets">Open Support Workspace</Link>
      </p>
      <ChatTestPanel />
    </main>
  )
}
