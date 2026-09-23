export interface TicketListItem {
  id: number
  subject: string
  status: string
  priority: string
  customerId: number
  customerName: string
}

export interface Ticket {
  id: number
  customerId: number
  subject: string
  status: string
  priority: string
  createdAt: string
}

export interface TicketMessage {
  id: number
  ticketId: number
  senderType: string
  message: string
  createdAt: string
}
