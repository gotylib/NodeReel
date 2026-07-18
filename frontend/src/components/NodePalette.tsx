import { useMemo, useState } from 'react'
import type { NodeDescriptor } from '../api'
import { NodeIcon, resolveNodeIcon } from './NodeIcon'

type Props = {
  nodes: NodeDescriptor[]
  collapsed: boolean
  onToggle: () => void
  onAdd: (descriptor: NodeDescriptor) => void
}

export function NodePalette({ nodes, collapsed, onToggle, onAdd }: Props) {
  const [query, setQuery] = useState('')

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return nodes
    return nodes.filter((n) =>
      [n.name, n.id, n.category, n.providerId, n.description, n.subtitle]
        .filter(Boolean)
        .some((v) => String(v).toLowerCase().includes(q)),
    )
  }, [nodes, query])

  const grouped = filtered.reduce<Record<string, NodeDescriptor[]>>((acc, node) => {
    const key = node.category || 'general'
    acc[key] ??= []
    acc[key].push(node)
    return acc
  }, {})

  if (collapsed) {
    return (
      <aside className="nr-palette collapsed">
        <button type="button" className="nr-palette-toggle" onClick={onToggle} title="Show nodes">
          ›
        </button>
      </aside>
    )
  }

  return (
    <aside className="nr-palette">
      <div className="nr-palette-head">
        <h2>Nodes</h2>
        <button type="button" className="nr-palette-toggle" onClick={onToggle} title="Hide panel">
          ‹
        </button>
      </div>
      <input
        className="nr-search"
        type="search"
        placeholder="Search nodes…"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
      />
      {Object.keys(grouped).length === 0 && (
        <p className="nr-muted">No nodes match “{query}”.</p>
      )}
      {Object.entries(grouped).map(([category, items]) => (
        <div key={category} className="nr-palette-group">
          <h3>{category}</h3>
          {items.map((node) => (
            <button
              key={`${node.providerId}:${node.id}`}
              type="button"
              className="nr-palette-item"
              onClick={() => onAdd(node)}
              title={node.description}
            >
              <NodeIcon name={resolveNodeIcon(node)} className="nr-palette-icon" />
              <span className="nr-palette-meta">
                <strong>{node.name}</strong>
                <span>{node.subtitle || node.providerId}</span>
              </span>
            </button>
          ))}
        </div>
      ))}
    </aside>
  )
}
