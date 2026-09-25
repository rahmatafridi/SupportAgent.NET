import { Navigate, Route, Routes } from 'react-router-dom'
import { HomePage } from './pages/HomePage'
import { TicketsPage } from './pages/TicketsPage'
import { TicketDetailPage } from './pages/TicketDetailPage'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { AdminUsagePage } from './pages/AdminUsagePage'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { KnowledgePage } from './pages/KnowledgePage'
import { PortalPage } from './pages/PortalPage'
import { PortalTicketPage } from './pages/PortalTicketPage'
import { DevAiTestPage } from './pages/DevAiTestPage'

export function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/tickets" element={<ProtectedRoute roles={['Admin', 'SupportAgent', 'Viewer']}><TicketsPage /></ProtectedRoute>} />
      <Route path="/tickets/:id" element={<ProtectedRoute roles={['Admin', 'SupportAgent', 'Viewer']}><TicketDetailPage /></ProtectedRoute>} />
      <Route path="/portal" element={<ProtectedRoute role="Customer"><PortalPage /></ProtectedRoute>} />
      <Route path="/portal/tickets/:id" element={<ProtectedRoute role="Customer"><PortalTicketPage /></ProtectedRoute>} />
      <Route path="/admin/usage" element={<ProtectedRoute role="Admin"><AdminUsagePage /></ProtectedRoute>} />
      <Route path="/knowledge" element={<ProtectedRoute roles={['Admin', 'SupportAgent']}><KnowledgePage /></ProtectedRoute>} />
      {import.meta.env.DEV && <Route path="/dev/ai-test" element={<ProtectedRoute role="Admin"><DevAiTestPage /></ProtectedRoute>} />}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
