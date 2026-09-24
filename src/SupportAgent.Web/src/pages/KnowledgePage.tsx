import { useCallback, useEffect, useState } from 'react'
import { AppHeader } from '../components/AppHeader'
import { KnowledgeUpload } from '../components/KnowledgeUpload'
import { KnowledgeDocumentList } from '../components/KnowledgeDocumentList'
import { deleteDocument, getDocuments, type KnowledgeDocument } from '../services/knowledgeService'
import { useAuth } from '../auth/AuthContext'

export function KnowledgePage() {
  const { user } = useAuth(); const isAdmin = user?.roles.includes('Admin') === true
  const [documents, setDocuments] = useState<KnowledgeDocument[]>([]); const [error, setError] = useState('')
  const load = useCallback(async () => { try { setDocuments(await getDocuments()); setError('') } catch (e) { setError((e as Error).message) } }, [])
  useEffect(() => { load() }, [load])
  async function remove(id: number) { if (!confirm('Delete this document and all of its knowledge chunks?')) return; try { await deleteDocument(id); await load() } catch (e) { setError((e as Error).message) } }
  return <><AppHeader /><main className="knowledge-page"><header><h1>Knowledge documents</h1><p>Uploaded content is extracted, embedded, and immediately available to the support AI.</p></header>
    {isAdmin && <KnowledgeUpload onUploaded={load} />}{error && <p className="panel-error">{error}</p>}
    <section className="knowledge-documents"><h2>Documents</h2><KnowledgeDocumentList documents={documents} canDelete={isAdmin} onDelete={remove} /></section>
  </main></>
}
