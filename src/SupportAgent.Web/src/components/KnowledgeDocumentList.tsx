import type { KnowledgeDocument } from '../services/knowledgeService'

const size = (bytes?: number) => bytes == null ? '—' : bytes < 1024 ? `${bytes} B` : `${(bytes / 1024).toFixed(1)} KB`
export function KnowledgeDocumentList({ documents, canDelete, onDelete }: { documents: KnowledgeDocument[]; canDelete: boolean; onDelete(id: number): void }) {
  if (!documents.length) return <p>No knowledge documents have been uploaded.</p>
  return <div className="knowledge-list">{documents.map(document => <article key={document.id}>
    <div><h3>{document.title}</h3><p>{document.originalFileName ?? 'Manually created document'}</p>
      <small>{document.processingStatus} · {document.chunkCount} chunks · {size(document.fileSizeBytes)} · {document.uploadedAt ? new Date(document.uploadedAt).toLocaleString() : 'Seeded content'}</small>
      {document.processingError && <p className="panel-error">{document.processingError}</p>}</div>
    {canDelete && <button className="danger-button" onClick={() => onDelete(document.id)}>Delete</button>}
  </article>)}</div>
}
