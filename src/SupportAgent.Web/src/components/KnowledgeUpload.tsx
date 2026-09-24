import { useState, type FormEvent } from 'react'
import { uploadDocument } from '../services/knowledgeService'

export function KnowledgeUpload({ onUploaded }: { onUploaded(): void }) {
  const [file, setFile] = useState<File | null>(null); const [title, setTitle] = useState('')
  const [busy, setBusy] = useState(false); const [message, setMessage] = useState(''); const [error, setError] = useState('')
  async function submit(event: FormEvent) {
    event.preventDefault(); if (!file) return; setBusy(true); setError(''); setMessage('')
    try { const result = await uploadDocument(file, title); setMessage(`${result.title} uploaded successfully (${result.chunksCreated} chunks).`); setFile(null); setTitle(''); onUploaded() }
    catch (e) { setError((e as Error).message); onUploaded() } finally { setBusy(false) }
  }
  return <form className="knowledge-upload" onSubmit={submit}><h2>Upload document</h2><p>PDF, DOCX, or UTF-8 TXT · maximum 10 MB</p>
    <label>Optional title<input value={title} onChange={e => setTitle(e.target.value)} /></label>
    <label>Document<input type="file" accept=".pdf,.docx,.txt,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,text/plain" onChange={e => setFile(e.target.files?.[0] ?? null)} required /></label>
    <button disabled={busy || !file}>{busy ? 'Processing…' : 'Upload document'}</button>
    {message && <p className="success-message">{message}</p>}{error && <p className="panel-error">{error}</p>}
  </form>
}
