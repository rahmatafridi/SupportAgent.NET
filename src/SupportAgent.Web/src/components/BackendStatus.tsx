import { useEffect, useState } from 'react'
import { checkHealth } from '../services/healthService'

export function BackendStatus() {
  const [isConnected, setIsConnected] = useState<boolean | null>(null)

  useEffect(() => {
    let isMounted = true

    checkHealth()
      .then((response) => {
        if (isMounted) {
          setIsConnected(response.status === 'ok')
        }
      })
      .catch(() => {
        if (isMounted) {
          setIsConnected(false)
        }
      })

    return () => {
      isMounted = false
    }
  }, [])

  if (isConnected === null) {
    return <p className="status status-checking">Backend Status: Checking...</p>
  }

  return (
    <p className={`status ${isConnected ? 'status-connected' : 'status-disconnected'}`}>
      Backend Status: {isConnected ? 'Connected' : 'Not Connected'}
    </p>
  )
}
