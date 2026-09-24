import { apiFetch } from './api'

export type KnowledgeDocument = {
  id: number; title: string; originalFileName?: string; contentType?: string; fileSizeBytes?: number;
  uploadedAt?: string; processingStatus: string; chunkCount: number; processingError?: string
}

export async function getDocuments(): Promise<KnowledgeDocument[]> {
  const response = await apiFetch('/api/knowledge/documents')
  if (!response.ok) throw new Error('Could not load knowledge documents.')
  return response.json()
}

export async function uploadDocument(file: File, title?: string) {
  const form = new FormData(); form.append('file', file); if (title?.trim()) form.append('title', title.trim())
  const response = await apiFetch('/api/knowledge/upload', { method: 'POST', body: form })
  if (!response.ok) { const body = await response.json().catch(() => ({})) as { error?: string }; throw new Error(body.error ?? 'Upload failed.') }
  return response.json()
}

export async function deleteDocument(id: number) {
  const response = await apiFetch(`/api/knowledge/documents/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new Error(response.status === 404 ? 'Document not found.' : 'Delete failed.')
}
