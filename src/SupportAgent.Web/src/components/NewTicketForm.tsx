import { useEffect, useState, type FormEvent, type MouseEvent } from 'react'
import { fetchCustomers } from '../services/customerService'
import { createTicket } from '../services/ticketService'
import type { Customer } from '../types/customer'

export function NewTicketForm({ onCreated, onCancel }: { onCreated(id: number): void; onCancel(): void }) {
  const [customers, setCustomers] = useState<Customer[]>([])
  const [customerId, setCustomerId] = useState('')
  const [subject, setSubject] = useState('')
  const [priority, setPriority] = useState('Medium')
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    fetchCustomers().then(items => { setCustomers(items); if (items[0]) setCustomerId(String(items[0].id)) }).catch(e => setError((e as Error).message))
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === 'Escape') onCancel() }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [onCancel])

  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try { const ticket = await createTicket({ customerId: Number(customerId), subject, priority, message }); onCreated(ticket.id) }
    catch (e) { setError((e as Error).message) } finally { setBusy(false) }
  }
  function closeBackdrop(event: MouseEvent<HTMLDivElement>) { if (event.target === event.currentTarget && !busy) onCancel() }

  return <div className="modal-backdrop" onMouseDown={closeBackdrop}>
    <section className="new-ticket-modal" role="dialog" aria-modal="true" aria-labelledby="new-ticket-title">
      <header className="modal-header"><div><span className="eyebrow">Support request</span><h2 id="new-ticket-title">Create a new ticket</h2><p>Start a customer conversation and assign its initial priority.</p></div><button type="button" className="icon-button" onClick={onCancel} aria-label="Close new ticket form">×</button></header>
      <form className="ticket-form" onSubmit={submit}>
        <label className="form-field form-field-wide"><span>Customer</span><select value={customerId} onChange={e => setCustomerId(e.target.value)} required><option value="" disabled>Select a customer</option>{customers.map(customer => <option key={customer.id} value={customer.id}>{customer.firstName} {customer.lastName} — {customer.email}</option>)}</select></label>
        <label className="form-field"><span>Subject</span><input maxLength={200} value={subject} onChange={e => setSubject(e.target.value)} placeholder="Briefly describe the issue" autoFocus required /></label>
        <label className="form-field"><span>Priority</span><select value={priority} onChange={e => setPriority(e.target.value)}><option>Low</option><option>Medium</option><option>High</option></select></label>
        <label className="form-field form-field-wide"><span>Initial message</span><textarea maxLength={4000} rows={5} value={message} onChange={e => setMessage(e.target.value)} placeholder="What did the customer report?" required /><small>{message.length}/4000 characters</small></label>
        {error && <p className="panel-error form-field-wide">{error}</p>}
        <footer className="modal-actions form-field-wide"><button type="button" className="button-secondary" onClick={onCancel} disabled={busy}>Cancel</button><button className="button-primary" disabled={busy || !customers.length}>{busy ? 'Creating…' : 'Create ticket'}</button></footer>
      </form>
    </section>
  </div>
}
