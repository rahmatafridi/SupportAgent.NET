import { apiFetch } from './api'; import type { InvoiceDetail, InvoiceListItem } from '../types/invoice';
async function read<T>(r:Response){if(!r.ok){let message=`Unable to load invoices (${r.status}).`;try{const body=await r.json() as{error?:string};if(body.error)message=body.error}catch{/* use clean status message */}throw new Error(message)}return r.json() as Promise<T>}
export const fetchInvoices=(portal=false)=>apiFetch(portal?'/api/portal/invoices/':'/api/invoices').then(read<InvoiceListItem[]>);
export const fetchInvoice=(id:number,portal=false)=>apiFetch(portal?`/api/portal/invoices/${id}`:`/api/invoices/${id}`).then(read<InvoiceDetail>);
export const invoicePdfUrl=(id:number,portal=false)=>portal?`/api/portal/invoices/${id}/pdf`:`/api/invoices/${id}/pdf`;
