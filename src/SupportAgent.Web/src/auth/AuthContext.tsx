import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import * as authService from '../services/authService'
import type { CurrentUser } from '../services/authService'

type AuthValue = {
  user: CurrentUser | null
  loading: boolean
  login(email: string, password: string): Promise<CurrentUser>
  register(organization: string, email: string, password: string): Promise<void>
  logout(): Promise<void>
}

const AuthContext = createContext<AuthValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [loading, setLoading] = useState(true)
  const refresh = async () => setUser(await authService.getCurrentUser())
  useEffect(() => { refresh().finally(() => setLoading(false)) }, [])
  return <AuthContext.Provider value={{
    user, loading,
    login: async (email, password) => { await authService.login(email, password); const current = await authService.getCurrentUser(); if (!current) throw new Error('Could not load the signed-in user.'); setUser(current); return current },
    register: async (organization, email, password) => { await authService.register(organization, email, password); await authService.login(email, password); await refresh() },
    logout: async () => { await authService.logout(); setUser(null) },
  }}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const value = useContext(AuthContext)
  if (!value) throw new Error('useAuth must be used inside AuthProvider.')
  return value
}
