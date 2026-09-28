import { useEffect, useState } from 'react'
import { AppHeader } from '../components/AppHeader'
import { apiFetch } from '../services/api'

type Usage = { totalRequests: number; inputTokens: number; outputTokens: number; totalTokens: number; breakdown: { provider: string; model: string; feature:string; requests: number; inputTokens:number; outputTokens:number; totalTokens: number }[] }
export function AdminUsagePage() {
  const [usage, setUsage] = useState<Usage | null>(null)
  useEffect(() => { apiFetch('/api/admin/ai-usage').then(r => { if (!r.ok) throw new Error(); return r.json() }).then(setUsage) }, [])
  return <><AppHeader /><main className="usage-page"><h1>AI usage</h1>{!usage ? <p>Loading…</p> : <><div className="usage-grid">
    <article><strong>{usage.totalRequests}</strong><span>Requests</span></article><article><strong>{usage.inputTokens}</strong><span>Input tokens</span></article>
    <article><strong>{usage.outputTokens}</strong><span>Output tokens</span></article><article><strong>{usage.totalTokens}</strong><span>Total tokens</span></article></div>
    <table><thead><tr><th>Provider</th><th>Model</th><th>Feature</th><th>Requests</th><th>Input</th><th>Output</th><th>Total</th></tr></thead><tbody>{usage.breakdown.map(x => <tr key={`${x.provider}-${x.model}-${x.feature}`}><td>{x.provider}</td><td>{x.model}</td><td>{x.feature}</td><td>{x.requests}</td><td>{x.inputTokens}</td><td>{x.outputTokens}</td><td>{x.totalTokens}</td></tr>)}</tbody></table></>}</main></>
}
