import { useState } from 'react'
import { api, type AuthUser } from '../api'

type Props = {
  onLoggedIn: (user: AuthUser, token: string) => void
}

export function LoginPage({ onLoggedIn }: Props) {
  const [username, setUsername] = useState('admin')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  return (
    <div className="nr-login">
      <form
        className="nr-login-card"
        onSubmit={async (e) => {
          e.preventDefault()
          try {
            setBusy(true)
            setError(null)
            const result = await api.login(username, password)
            onLoggedIn(result.user, result.token)
          } catch (err) {
            setError(err instanceof Error ? err.message : String(err))
          } finally {
            setBusy(false)
          }
        }}
      >
        <div className="nr-login-brand">
          <span className="nr-logo">NodeReel</span>
          <p>Sign in to your media pipelines</p>
        </div>
        <label className="nr-field">
          <span>Username</span>
          <input
            autoFocus
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
          />
        </label>
        <label className="nr-field">
          <span>Password</span>
          <input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
        {error && <p className="nr-error">{error}</p>}
        <button type="submit" className="primary" disabled={busy}>
          {busy ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  )
}
