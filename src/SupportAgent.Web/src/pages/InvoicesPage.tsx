import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { AppHeader } from '../components/AppHeader'
import { fetchInvoices } from '../services/invoiceService'
import type { InvoiceListItem } from '../types/invoice'

export function InvoicesPage({ portal = false }: { portal?: boolean }) {
  const [invoices, setInvoices] = useState<InvoiceListItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    setLoading(true)
    setError(null)
    fetchInvoices(portal)
      .then(result => { if (active) setInvoices(result) })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'Unable to load invoices.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [portal])

  return <>
    <AppHeader />
    <main className="portal-page">
      <h1>{portal ? 'My Invoices' : 'Invoices'}</h1>
      {loading && <p>Loading invoices...</p>}
      {!loading && error && <p className="panel-error">{error}</p>}
      {!loading && !error && invoices.length === 0 && <p>No invoices found.</p>}
      {!loading && !error && invoices.length > 0 && <div className="portal-ticket-list">
        {invoices.map(invoice => <Link key={invoice.id} to={`${portal ? '/portal' : ''}/invoices/${invoice.id}`}>
          <strong>{invoice.invoiceNumber} · {invoice.customerName}</strong>
          <span>{invoice.status}</span>
          <small>{new Date(invoice.issueDate).toLocaleDateString()} · Total ${invoice.totalAmount.toFixed(2)} · Outstanding ${invoice.outstandingBalance.toFixed(2)}</small>
        </Link>)}
      </div>}
    </main>
  </>
}
