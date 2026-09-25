import type { KnowledgeSource, ToolUsage } from './chat'

export interface CopilotAskRequest {
  ticketId: number
  conversationId: string | null
  message: string
}

export interface CopilotAskResponse {
  conversationId: string
  answer: string
  suggestedActions: AISuggestedAction[]
  confidence: number
  toolsUsed: ToolUsage[]
  sources: KnowledgeSource[]
}

export interface AISuggestedAction {
  id: string
  label: string
  description: string | null
  actionType: 'GetCustomerOrders' | 'GetOrderStatus' | 'SearchKnowledgeBase' | 'ViewRecentTickets' | 'LinkOrder'
  requiresConfirmation: boolean
  status: 'Pending' | 'Completed' | 'Failed'
}

export interface ActionOrderResult {
  id: number
  orderNumber: string
  status: string
  totalAmount: number
  createdAt: string
  linkActionId: string
}

export interface CopilotActionExecutionResponse {
  action: AISuggestedAction['actionType']
  actionLabel: string
  success: boolean
  result: {
    orders?: ActionOrderResult[]
    linkedOrderId?: number
    order?: Omit<ActionOrderResult, 'linkActionId'>
  }
  updatedResponse: CopilotAskResponse
}

export interface CopilotDraftRequest {
  ticketId: number
}

export interface CopilotDraftBody {
  subject: string
  body: string
  tone: string
  confidence: number
}

export interface CopilotDraftResponse {
  draft: CopilotDraftBody
  toolsUsed: ToolUsage[]
  sources: KnowledgeSource[]
}
