import { useEffect, useState } from 'react'
import { askCopilot, draftCopilotReply, executeCopilotAction } from '../services/copilotService'
import type { ActionOrderResult, CopilotAskResponse, CopilotDraftResponse } from '../types/copilot'

interface CopilotPanelProps {
  ticketId: number
  onUseDraft?(text: string): void
}

export function CopilotPanel({ ticketId, onUseDraft }: CopilotPanelProps) {
  const [conversationId, setConversationId] = useState<string | null>(null)
  const [question, setQuestion] = useState('What is happening with this customer?')
  const [answer, setAnswer] = useState<CopilotAskResponse | null>(null)
  const [draft, setDraft] = useState<CopilotDraftResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isAsking, setIsAsking] = useState(false)
  const [isDrafting, setIsDrafting] = useState(false)
  const [executingActionId, setExecutingActionId] = useState<string | null>(null)
  const [candidateOrders, setCandidateOrders] = useState<ActionOrderResult[]>([])
  const [performedActions, setPerformedActions] = useState<string[]>([])
  const [draftNotice, setDraftNotice] = useState<string | null>(null)
  const [lastSubmittedQuestion, setLastSubmittedQuestion] = useState<string | null>(null)

  useEffect(() => {
    setConversationId(null)
    setAnswer(null)
    setDraft(null)
    setError(null)
    setQuestion('What is happening with this customer?')
    setCandidateOrders([])
    setPerformedActions([])
    setDraftNotice(null)
    setLastSubmittedQuestion(null)
  }, [ticketId])

  async function handleSuggestedAction(action: CopilotAskResponse['suggestedActions'][number]) {
    if (!conversationId) return
    const confirmed = !action.requiresConfirmation || window.confirm(`${action.label}?\n\n${action.description ?? 'This action changes ticket data.'}`)
    if (!confirmed) return
    setExecutingActionId(action.id)
    setError(null)
    try {
      const execution = await executeCopilotAction(action.id, ticketId, conversationId, confirmed)
      setAnswer(execution.updatedResponse)
      setConversationId(execution.updatedResponse.conversationId)
      setPerformedActions(current => [...current, execution.actionLabel])
      setLastSubmittedQuestion(null)
      if (execution.result.orders) setCandidateOrders(execution.result.orders)
      if (execution.result.linkedOrderId) setCandidateOrders([])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Suggested action failed.')
    } finally {
      setExecutingActionId(null)
    }
  }

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
      setLastSubmittedQuestion(question.trim())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Copilot request failed.')
    } finally {
      setIsAsking(false)
    }
  }

  async function handleDraftReply() {
    setIsDrafting(true)
    setError(null)
    setDraftNotice(null)

    try {
      const result = await draftCopilotReply({ ticketId })
      setDraft(result)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Draft reply failed.')
    } finally {
      setIsDrafting(false)
    }
  }

  function handleUseDraft() {
    if (!draft || !onUseDraft) return
    onUseDraft(draft.draft.body)
    setDraftNotice('Draft added to Agent Reply. Review it before sending.')
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
        <button type="submit" disabled={isAsking || (answer !== null && question.trim() === lastSubmittedQuestion)}>
          {isAsking ? 'Thinking...' : answer !== null && question.trim() === lastSubmittedQuestion ? 'Answered' : 'Send'}
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
          <p className="draft-meta">Confidence: {answer.confidence.toFixed(2)}</p>
          {answer.suggestedActions.some(action => action.actionType !== 'LinkOrder') && <div className="suggested-actions"><strong>Suggested actions</strong><div className="action-card-list">{answer.suggestedActions.filter(action => action.actionType !== 'LinkOrder').map(action => <button type="button" className="suggested-action-card" key={action.id} disabled={executingActionId !== null} onClick={() => handleSuggestedAction(action)}><span>{executingActionId === action.id ? 'Working...' : action.label}</span>{action.description && <small>{action.description}</small>}{action.requiresConfirmation && <small>Confirmation required</small>}</button>)}</div><small>Actions run only when you click them.</small></div>}
          {candidateOrders.length > 0 && (
            <div className="suggested-actions">
              <strong>Customer orders</strong>
              <div className="action-card-list">
                {candidateOrders.map(order => {
                  const linkAction = answer.suggestedActions.find(action => action.id === order.linkActionId)
                  return <div className="suggested-action-card" key={order.id}>
                    <strong>{order.orderNumber}</strong>
                    <small>Status: {order.status}</small>
                    <small>Amount: {new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(order.totalAmount)}</small>
                    {linkAction && <button type="button" disabled={executingActionId !== null} onClick={() => handleSuggestedAction(linkAction)}>{executingActionId === linkAction.id ? 'Linking...' : 'Link this order'}</button>}
                  </div>
                })}
              </div>
            </div>
          )}
          {performedActions.length > 0 && (
            <div className="chat-tools">
              <strong>Actions performed:</strong>
              <ul>{performedActions.map((label, index) => <li key={`${label}-${index}`}>{label}</li>)}</ul>
            </div>
          )}
          {answer.toolsUsed.length > 0 && (
            <div className="chat-tools">
              <strong>AI tools used:</strong>
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
          {answer.sources.length === 0 && <p className="empty-copy">No AI sources.</p>}
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
          {onUseDraft && <button type="button" className="secondary-button" onClick={handleUseDraft}>Use Draft</button>}
          {draftNotice && <p className="panel-success">{draftNotice}</p>}
        </div>
      )}
    </aside>
  )
}
