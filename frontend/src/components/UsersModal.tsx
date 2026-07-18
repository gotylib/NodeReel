import { useEffect, useState } from 'react'
import { api, getStoredUser, type AuthUser } from '../api'

type Props = {
  open: boolean
  onClose: () => void
}

export function UsersModal({ open, onClose }: Props) {
  const [users, setUsers] = useState<AuthUser[]>([])
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState<'User' | 'Admin'>('User')
  const [error, setError] = useState<string | null>(null)
  const [ok, setOk] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [passwordUserId, setPasswordUserId] = useState<string | null>(null)
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const me = getStoredUser()

  const load = async () => {
    setUsers(await api.listUsers())
  }

  useEffect(() => {
    if (open) {
      setError(null)
      setOk(null)
      setPasswordUserId(null)
      setNewPassword('')
      setConfirmPassword('')
      void load().catch((e) => setError(String(e)))
    }
  }, [open])

  if (!open) return null

  return (
    <div className="nr-modal-backdrop" onClick={onClose}>
      <div className="nr-modal" onClick={(e) => e.stopPropagation()}>
        <header>
          <h2>Users</h2>
          <button type="button" onClick={onClose}>Close</button>
        </header>

        <p className="nr-muted">
          Create accounts and change passwords (including your own). Each user only sees their own pipelines.
        </p>

        <div className="nr-form-row">
          <input value={username} onChange={(e) => setUsername(e.target.value)} placeholder="Username" />
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="Password"
          />
          <select value={role} onChange={(e) => setRole(e.target.value as 'User' | 'Admin')}>
            <option value="User">User</option>
            <option value="Admin">Admin</option>
          </select>
          <button
            type="button"
            disabled={busy}
            onClick={async () => {
              try {
                setBusy(true)
                setError(null)
                setOk(null)
                await api.createUser({ username, password, role })
                setUsername('')
                setPassword('')
                setRole('User')
                setOk('User created')
                await load()
              } catch (e) {
                setError(String(e))
              } finally {
                setBusy(false)
              }
            }}
          >
            Create
          </button>
        </div>

        {error && <p className="nr-error">{error}</p>}
        {ok && <p className="nr-ok">{ok}</p>}

        <ul className="nr-provider-list">
          {users.map((u) => {
            const isMe = me?.id === u.id
            const editing = passwordUserId === u.id
            return (
              <li key={u.id} className={editing ? 'nr-user-row-edit' : undefined}>
                <div>
                  <strong>
                    {u.username}
                    {isMe ? ' (you)' : ''}
                  </strong>
                  <span>{u.role}</span>
                </div>
                <div className="nr-user-actions">
                  <button
                    type="button"
                    onClick={() => {
                      setError(null)
                      setOk(null)
                      if (editing) {
                        setPasswordUserId(null)
                        setNewPassword('')
                        setConfirmPassword('')
                      } else {
                        setPasswordUserId(u.id)
                        setNewPassword('')
                        setConfirmPassword('')
                      }
                    }}
                  >
                    {editing ? 'Cancel' : 'Password'}
                  </button>
                  {!isMe && (
                    <button
                      type="button"
                      onClick={async () => {
                        try {
                          setError(null)
                          setOk(null)
                          await api.deleteUser(u.id)
                          if (passwordUserId === u.id) setPasswordUserId(null)
                          setOk(`Deleted ${u.username}`)
                          await load()
                        } catch (e) {
                          setError(String(e))
                        }
                      }}
                    >
                      Delete
                    </button>
                  )}
                </div>
                {editing && (
                  <div className="nr-password-edit">
                    <input
                      type="password"
                      value={newPassword}
                      onChange={(e) => setNewPassword(e.target.value)}
                      placeholder="New password"
                      autoComplete="new-password"
                    />
                    <input
                      type="password"
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      placeholder="Confirm password"
                      autoComplete="new-password"
                    />
                    <button
                      type="button"
                      disabled={busy}
                      onClick={async () => {
                        if (newPassword.length < 4) {
                          setError('Password must be at least 4 characters.')
                          return
                        }
                        if (newPassword !== confirmPassword) {
                          setError('Passwords do not match.')
                          return
                        }
                        try {
                          setBusy(true)
                          setError(null)
                          await api.changeUserPassword(u.id, newPassword)
                          setPasswordUserId(null)
                          setNewPassword('')
                          setConfirmPassword('')
                          setOk(`Password updated for ${u.username}`)
                        } catch (e) {
                          setError(String(e))
                        } finally {
                          setBusy(false)
                        }
                      }}
                    >
                      Save password
                    </button>
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      </div>
    </div>
  )
}
