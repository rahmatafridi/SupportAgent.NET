export interface ChatRequest {
  message: string
}

export interface ToolUsage {
  name: string
  arguments: Record<string, unknown>
}

export interface KnowledgeSource {
  title: string
  source?: string | null
}

export interface ChatResponse {
  text: string
  provider: string
  model: string
  totalTokens: number
  durationMs: number
  toolsUsed?: ToolUsage[]
  sources?: KnowledgeSource[]
}
