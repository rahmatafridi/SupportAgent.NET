import { useEffect, useState } from 'react'
import { askCopilot, draftCopilotReply } from '../services/copilotService'
import type { CopilotAskResponse, CopilotDraftResponse } from '../types/copilot'

interface CopilotPanelProps {
  ticketId: number
}

export function CopilotPanel({ ticketId }: CopilotPanelProps) {
  const [conversationId, setConversationId] = useState<string | null>(null)
  const [question, setQuestion] = useState('What is happening with this customer?')
  const [answer, setAnswer] = useState<CopilotAskResponse | null>(null)
  const [draft, setDraft] = useState<CopilotDraftResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isAsking, setIsAsking] = useState(false)
  const [isDrafting, setIsDrafting] = useState(false)

  useEffect(() => {
    setConversationId(null)
    setAnswer(null)
    setDraft(null)
    setError(null)
    setQuestion('What is happening with this customer?')
  }, [ticketId])

  async function handleAsk(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!question.trim()) {
      setError('Please enter a question.')
      return
    }

    setIsAsking(true)
    setError(null)

    try {
      const result = await askCopilot({
        ticketId,
        conversationId,
        message: question.trim(),
      })
      setAnswer(result)
      setConversationId(result.conversationId)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Copilot request failed.')
    } finally {
      setIsAsking(false)
    }
  }

  async function handleDraftReply() {
    setIsDrafting(true)
    setError(null)

    try {
      const result = await draftCopilotReply({ ticketId })
      setDraft(result)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Draft reply failed.')
    } finally {
      setIsDrafting(false)
    }
  }

  async function handleCopyDraft() {
    if (!draft) {
      return
    }

    const text = draft.draft.subject
      ? `Subject: ${draft.draft.subject}\n\n${draft.draft.body}`
      : draft.draft.body

    await navigator.clipboard.writeText(text)
  }

  return (
    <aside className="copilot-panel">
      <div className="panel-header">
        <h2>AI Assistant</h2>
        <p className="panel-help">Suggested replies are not sent automatically.</p>
      </div>

      <form className="copilot-form" onSubmit={handleAsk}>
        <textarea
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          rows={4}
          placeholder="Ask the copilot..."
        />
        <button type="submit" disabled={isAsking}>
          {isAsking ? 'Thinking...' : 'Send'}
        </button>
      </form>

      <button
        type="button"
        className="secondary-button"
        onClick={handleDraftReply}
        disabled={isDrafting}
      >
        {isDrafting ? 'Generating...' : 'Generate Reply'}
      </button>

      {conversationId && (
        <p className="conversation-id">Conversation: {conversationId}</p>
      )}

      {error && <p className="panel-error">{error}</p>}

      {answer && (
        <div className="copilot-answer">
          <h3>Answer</h3>
          <p>{answer.answer}</p>
          {answer.toolsUsed.length > 0 && (
            <div className="chat-tools">
              <strong>Tools used:</strong>
              <ul>
                {answer.toolsUsed.map((tool) => (
                  <li key={`${tool.name}-${JSON.stringify(tool.arguments)}`}>
                    {tool.name}({JSON.stringify(tool.arguments)})
                  </li>
                ))}
              </ul>
            </div>
          )}
          {answer.sources.length > 0 && (
            <div className="chat-sources">
              <strong>Sources:</strong>
              <ul>
                {answer.sources.map((source) => (
                  <li key={`${source.title}-${source.source ?? 'unknown'}`}>
                    {source.title}
                    {source.source ? ` (${source.source})` : ''}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}

      {draft && (
        <div className="copilot-draft">
          <h3>Suggested Reply</h3>
          {draft.draft.subject && <p><strong>Subject:</strong> {draft.draft.subject}</p>}
          <p>{draft.draft.body}</p>
          <p className="draft-meta">
            Tone: {draft.draft.tone} · Confidence: {draft.draft.confidence.toFixed(2)}
          </p>
          <button type="button" className="secondary-button" onClick={handleCopyDraft}>
            Copy Reply
          </button>
        </div>
      )}
    </aside>
  )
}
