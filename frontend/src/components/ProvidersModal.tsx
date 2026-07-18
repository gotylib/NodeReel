import { useEffect, useState } from 'react'
import { api, type NodeProvider } from '../api'

type Props = {
  open: boolean
  onClose: () => void
  onChanged: () => void
}

export function ProvidersModal({ open, onClose, onChanged }: Props) {
  const [providers, setProviders] = useState<NodeProvider[]>([])
  const [name, setName] = useState('Custom server')
  const [baseUrl, setBaseUrl] = useState('http://localhost:5088')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const load = async () => {
    setProviders(await api.getProviders())
  }

  useEffect(() => {
    if (open) void load().catch((e) => setError(String(e)))
  }, [open])

  if (!open) return null

  return (
    <div className="nr-modal-backdrop" onClick={onClose}>
      <div className="nr-modal" onClick={(e) => e.stopPropagation()}>
        <header>
          <h2>Node providers</h2>
          <button type="button" onClick={onClose}>Close</button>
        </header>

        <p className="nr-muted">
          Connect an external node server that exposes <code>GET /nodes</code> and{' '}
          <code>POST /execute</code>. Files stay in shared MinIO.
        </p>

        <div className="nr-form-row">
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Name" />
          <input value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} placeholder="http://localhost:5088" />
          <button
            type="button"
            disabled={busy}
            onClick={async () => {
              try {
                setBusy(true)
                setError(null)
                await api.createProvider({ name, baseUrl })
                await api.refreshNodes()
                await load()
                onChanged()
              } catch (e) {
                setError(String(e))
              } finally {
                setBusy(false)
              }
            }}
          >
            Add
          </button>
        </div>

        {error && <p className="nr-error">{error}</p>}

        <ul className="nr-provider-list">
          {providers.map((p) => (
            <li key={p.id}>
              <div>
                <strong>{p.name}</strong>
                <span>{p.baseUrl}</span>
              </div>
              <button
                type="button"
                onClick={async () => {
                  await api.deleteProvider(p.id)
                  await api.refreshNodes()
                  await load()
                  onChanged()
                }}
              >
                Remove
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
