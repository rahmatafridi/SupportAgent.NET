import { apiFetch } from './api'
import type { Customer } from '../types/customer'

export async function fetchCustomers(): Promise<Customer[]> {
  const response = await apiFetch('/api/customers')
  if (!response.ok) throw new Error('Could not load customers.')
  return response.json()
}
