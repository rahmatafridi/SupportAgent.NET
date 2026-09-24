export interface TicketListItem {
  id: number
  subject: string
  status: string
  priority: string
  customerId: number
  customerName: string
  customerEmail: string
  assignedToUserId: string | null
  assignedToName: string | null
  createdAt: string
  updatedAt: string
}

export interface Ticket {
  id: number
  customerId: number
  subject: string
  status: string
  priority: string
  createdAt: string
  updatedAt: string
  assignedToUserId: string | null
}

export interface TicketFilters { search: string; status: string; priority: string; sort: string; assignedToMe: boolean }
export interface TicketAssignee { id: string; displayName: string; email: string }
export interface TicketInternalNote { id: number; ticketId: number; authorUserId: string; content: string; createdAt: string }
export interface TicketSummary { open: number; inProgress: number; highPriority: number; unassigned: number; closedToday: number }
export interface RecentOrder { id: number; orderNumber: string; status: string; totalAmount: number; createdAt: string }
export interface RecentTicket { id: number; subject: string; status: string; priority: string; updatedAt: string }
export interface TicketCustomerContext { id: number; name: string; email: string; phone: string | null; recentOrders: RecentOrder[]; recentTickets: RecentTicket[] }

export interface TicketMessage {
  id: number
  ticketId: number
  senderType: string
  message: string
  createdAt: string
}
