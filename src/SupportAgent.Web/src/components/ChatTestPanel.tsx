import { useState } from 'react'
import { sendChatMessage } from '../services/chatService'
import type { ChatResponse } from '../types/chat'

export function ChatTestPanel() {
  const [message, setMessage] = useState('Who is customer 101?')
  const [response, setResponse] = useState<ChatResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!message.trim()) {
      setError('Please enter a message.')
      return
    }

    setIsLoading(true)
    setError(null)
    setResponse(null)

    try {
      const result = await sendChatMessage(message.trim())
      setResponse(result)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Chat request failed.')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <section className="chat-panel">
      <h2>AI Chat Test</h2>
      <p className="chat-help">
        Sends a request to <code>POST /api/ai/chat</code>. Requires Ollama running locally.
      </p>

      <form className="chat-form" onSubmit={handleSubmit}>
        <textarea
          value={message}
          onChange={(event) => setMessage(event.target.value)}
          rows={4}
          placeholder="Ask the AI gateway something..."
        />
        <button type="submit" disabled={isLoading}>
          {isLoading ? 'Sending...' : 'Send Message'}
        </button>
      </form>

      {error && <p className="chat-error">{error}</p>}

      {response && (
        <div className="chat-response">
          <p>{response.text}</p>
          {response.toolsUsed && response.toolsUsed.length > 0 && (
            <div className="chat-tools">
              <strong>Tools used:</strong>
              <ul>
                {response.toolsUsed.map((tool) => (
                  <li key={`${tool.name}-${JSON.stringify(tool.arguments)}`}>
                    {tool.name}({JSON.stringify(tool.arguments)})
                  </li>
                ))}
              </ul>
            </div>
          )}
          {response.sources && response.sources.length > 0 && (
            <div className="chat-sources">
              <strong>Sources:</strong>
              <ul>
                {response.sources.map((source) => (
                  <li key={`${source.title}-${source.source ?? 'unknown'}`}>
                    {source.title}
                    {source.source ? ` (${source.source})` : ''}
                  </li>
                ))}
              </ul>
            </div>
          )}
          <p className="chat-meta">
            {response.provider} · {response.model} · {response.totalTokens} tokens ·{' '}
            {response.durationMs} ms
          </p>
        </div>
      )}
    </section>
  )
}
