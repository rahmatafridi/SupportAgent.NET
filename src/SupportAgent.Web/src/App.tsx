import { Navigate, Route, Routes } from 'react-router-dom'
import { HomePage } from './pages/HomePage'
import { TicketsPage } from './pages/TicketsPage'
import { TicketDetailPage } from './pages/TicketDetailPage'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { AdminUsagePage } from './pages/AdminUsagePage'
import { ProtectedRoute } from './auth/ProtectedRoute'

export function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/tickets" element={<ProtectedRoute><TicketsPage /></ProtectedRoute>} />
      <Route path="/tickets/:id" element={<ProtectedRoute><TicketDetailPage /></ProtectedRoute>} />
      <Route path="/admin/usage" element={<ProtectedRoute role="Admin"><AdminUsagePage /></ProtectedRoute>} />
      <Route path="*" element={<Navigate to="/tickets" replace />} />
    </Routes>
  )
}
