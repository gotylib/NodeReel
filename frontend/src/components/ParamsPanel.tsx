import type { Node } from '@xyflow/react'
import { api, type PipelineRunResult } from '../api'
import type { GenericNodeData } from './GenericNode'

type Props = {
  node: Node | null
  running?: boolean
  runResult?: PipelineRunResult | null
  onChange: (nodeId: string, params: Record<string, string | number>) => void
  onUpload: (nodeId: string, file: File) => Promise<void>
  onDelete: (nodeId: string) => void
  onDuplicate: (nodeId: string) => void
  onRun: () => void
}

function mediaKind(name: string, type?: string): 'video' | 'audio' | 'image' | 'file' {
  const t = (type ?? '').toLowerCase()
  const n = name.toLowerCase()
  if (t.includes('video') || n === 'video' || n === 'output' || n === 'true' || n === 'false') return 'video'
  if (t.includes('audio') || n === 'audio') return 'audio'
  if (t.includes('image') || n === 'image') return 'image'
  return 'file'
}

function downloadName(port: string, kind: 'video' | 'audio' | 'image' | 'file') {
  const ext = kind === 'audio' ? 'mp3' : kind === 'image' ? 'png' : kind === 'video' ? 'mp4' : 'bin'
  return `${port}.${ext}`
}

export function ParamsPanel({
  node,
  running,
  runResult,
  onChange,
  onUpload,
  onDelete,
  onDuplicate,
  onRun,
}: Props) {
  if (!node) {
    return (
      <aside className="nr-panel">
        <h2>Parameters</h2>
        <p className="nr-muted">Select a node to edit parameters.</p>
      </aside>
    )
  }

  const data = node.data as GenericNodeData
  const properties = (data.descriptor.paramsSchema?.properties ?? {}) as Record<
    string,
    {
      type?: string
      minimum?: number
      maximum?: number
      default?: number | string
      description?: string
      enum?: string[]
    }
  >
  const entries = Object.entries(properties)
  const step = runResult?.steps.find((s) => s.nodeInstanceId === node.id)
  const outputEntries = Object.entries(step?.outputs ?? {})
  const isUploadNode =
    data.descriptor.id === 'upload-video' ||
    data.descriptor.id === 'upload-image' ||
    data.descriptor.id === 'upload-audio'
  const uploadKey = isUploadNode ? String(data.params.objectKey ?? '') : ''
  const uploadKind =
    data.descriptor.id === 'upload-image' ? 'image' : data.descriptor.id === 'upload-audio' ? 'audio' : 'video'

  return (
    <aside className="nr-panel">
      <h2>{data.label}</h2>
      <p className="nr-muted">{data.descriptor.description}</p>

      <div className="nr-node-actions">
        <button type="button" className="nr-action-run" disabled={running} onClick={onRun} title="Run pipeline">
          <span className="nr-play-icon" aria-hidden />
          Run
        </button>
        <button type="button" onClick={() => onDuplicate(node.id)} title="Duplicate node">
          Duplicate
        </button>
        <button type="button" className="danger" onClick={() => onDelete(node.id)} title="Delete node (Del)">
          Delete
        </button>
      </div>

      {data.runStatus === 'Failed' && data.runError && (
        <div className="nr-run-error">
          <strong>Error</strong>
          <p>{data.runError}</p>
        </div>
      )}

      {(outputEntries.length > 0 || uploadKey) && (
        <div className="nr-output-preview">
          <h3>Result</h3>
          {uploadKey && outputEntries.length === 0 && (
            <div className="nr-media-card">
              {uploadKind === 'image' && (
                <img key={uploadKey} src={api.downloadUrl(uploadKey)} alt="Uploaded" />
              )}
              {uploadKind === 'video' && (
                <video key={uploadKey} src={api.downloadUrl(uploadKey)} controls preload="metadata" />
              )}
              {uploadKind === 'audio' && (
                <audio key={uploadKey} src={api.downloadUrl(uploadKey)} controls preload="metadata" />
              )}
              <a href={api.downloadUrl(uploadKey, true)} download={downloadName('upload', uploadKind)}>
                Download {uploadKind}
              </a>
            </div>
          )}
          {outputEntries.map(([port, key]) => {
            const portMeta = data.descriptor.outputs.find((p) => p.name === port)
            const kind = mediaKind(port, portMeta?.type)
            return (
              <div key={port} className="nr-media-card">
                <div className="nr-media-meta">
                  <code>{port}</code>
                  <span className="nr-muted">{key}</span>
                </div>
                {kind === 'video' && (
                  <video key={key} src={api.downloadUrl(key)} controls preload="metadata" />
                )}
                {kind === 'audio' && (
                  <audio key={key} src={api.downloadUrl(key)} controls preload="metadata" />
                )}
                {kind === 'image' && (
                  <img key={key} src={api.downloadUrl(key)} alt={port} />
                )}
                <a href={api.downloadUrl(key, true)} download={downloadName(port, kind)}>
                  Download {port}
                </a>
              </div>
            )
          })}
        </div>
      )}

      <div className="nr-io">
        <h3>Accepts</h3>
        {data.descriptor.inputs.length === 0 ? (
          <p className="nr-muted">No inputs (source node).</p>
        ) : (
          <ul>
            {data.descriptor.inputs.map((p) => (
              <li key={`in-${p.name}`}>
                <code>{p.name}</code>
                <span>{p.type}{p.required ? '' : ' · optional'}</span>
              </li>
            ))}
          </ul>
        )}
        <h3>Outputs</h3>
        {data.descriptor.outputs.length === 0 ? (
          <p className="nr-muted">No outputs.</p>
        ) : (
          <ul>
            {data.descriptor.outputs.map((p) => (
              <li key={`out-${p.name}`}>
                <code>{p.name}</code>
                <span>{p.type}</span>
              </li>
            ))}
          </ul>
        )}
      </div>

      {isUploadNode && (
        <label className="nr-field">
          <span>
            {uploadKind === 'image' ? 'Image file' : uploadKind === 'audio' ? 'Audio file' : 'Video file'}
          </span>
          <input
            type="file"
            accept={uploadKind === 'image' ? 'image/*' : uploadKind === 'audio' ? 'audio/*' : 'video/*'}
            onChange={async (e) => {
              const file = e.target.files?.[0]
              if (file) await onUpload(node.id, file)
            }}
          />
          {data.params.objectKey ? (
            <small className="nr-ok">Uploaded: {String(data.params.objectKey)}</small>
          ) : (
            <small className="nr-warn">Upload a file before running.</small>
          )}
        </label>
      )}

      {entries.length === 0 && !isUploadNode && (
        <p className="nr-muted">No parameters.</p>
      )}

      {entries.map(([key, schema]) => {
        if (isUploadNode && key === 'objectKey') return null
        const value = data.params[key] ?? schema.default ?? ''
        const options = schema.enum
        return (
          <label key={key} className="nr-field">
            <span>{key}</span>
            {options && options.length > 0 ? (
              <select
                value={String(value)}
                onChange={(e) => {
                  const next = { ...data.params }
                  next[key] = e.target.value
                  onChange(node.id, next)
                }}
              >
                {options.map((opt) => (
                  <option key={opt} value={opt}>
                    {opt}
                  </option>
                ))}
              </select>
            ) : (
              <input
                type={schema.type === 'number' ? 'number' : 'text'}
                min={schema.minimum}
                max={schema.maximum}
                value={value}
                onChange={(e) => {
                  const next = { ...data.params }
                  next[key] = schema.type === 'number' ? Number(e.target.value) : e.target.value
                  onChange(node.id, next)
                }}
              />
            )}
            {schema.description && <small>{schema.description}</small>}
          </label>
        )
      })}
    </aside>
  )
}

