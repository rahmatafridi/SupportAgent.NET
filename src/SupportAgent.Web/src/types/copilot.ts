import type { KnowledgeSource, ToolUsage } from './chat'

export interface CopilotAskRequest {
  ticketId: number
  conversationId: string | null
  message: string
}

export interface CopilotAskResponse {
  conversationId: string
  answer: string
  suggestedActions: string[]
  confidence: number
  toolsUsed: ToolUsage[]
  sources: KnowledgeSource[]
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
