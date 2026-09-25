export interface PortalProfile { customerId: number; name: string; email: string; phone: string | null; organizationName: string }
export interface PortalTicket { id: number; subject: string; status: string; priority: string; createdAt: string; updatedAt: string; orderId: number | null; hasUnreadAgentReply: boolean }
export interface PortalTicketList { tickets: PortalTicket[]; summary: { open: number; inProgress: number; closed: number } }
export interface PortalMessage { id: number; senderType: string; message: string; createdAt: string }
export interface PortalOrder { id: number; orderNumber: string; status: string; totalAmount: number; createdAt: string }
