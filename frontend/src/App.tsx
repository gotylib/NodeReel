import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  Background,
  Controls,
  MiniMap,
  ReactFlow,
  addEdge,
  useEdgesState,
  useNodesState,
  type Connection,
  type Edge,
  type EdgeTypes,
  type Node,
  type NodeTypes,
} from '@xyflow/react'
import '@xyflow/react/dist/style.css'
import {
  api,
  clearSession,
  getStoredToken,
  getStoredUser,
  setSession,
  type AuthUser,
  type NodeDescriptor,
  type PipelineRunResult,
  type WorkflowSummary,
} from './api'
import { DeletableEdge } from './components/DeletableEdge'
import { GenericNode, type GenericNodeData } from './components/GenericNode'
import { LoginPage } from './components/LoginPage'
import { NodeContextMenu, type NodeContextMenuState } from './components/NodeContextMenu'
import { NodePalette } from './components/NodePalette'
import { ParamsPanel } from './components/ParamsPanel'
import { ProvidersModal } from './components/ProvidersModal'
import { UsersModal } from './components/UsersModal'

const nodeTypes: NodeTypes = { genericNode: GenericNode }
const edgeTypes: EdgeTypes = { deletable: DeletableEdge }

type ClipboardNode = {
  type?: string
  data: GenericNodeData
  width?: number
  height?: number
}

function defaultParams(descriptor: NodeDescriptor): Record<string, string | number> {
  const params: Record<string, string | number> = {}
  const props = descriptor.paramsSchema?.properties ?? {}
  for (const [key, schema] of Object.entries(props)) {
    if (schema.default !== undefined) params[key] = schema.default
  }
  return params
}

type GraphPayload = {
  nodes: Node[]
  edges: Edge[]
}

export default function App() {
  const [user, setUser] = useState<AuthUser | null>(() => (getStoredToken() ? getStoredUser() : null))
  const [authChecking, setAuthChecking] = useState(() => Boolean(getStoredToken()))

  useEffect(() => {
    const onUnauthorized = () => setUser(null)
    window.addEventListener('nr:unauthorized', onUnauthorized)
    return () => window.removeEventListener('nr:unauthorized', onUnauthorized)
  }, [])

  useEffect(() => {
    if (!getStoredToken()) {
      setAuthChecking(false)
      return
    }
    void api.me()
      .then((me) => {
        setUser(me)
        const token = getStoredToken()
        if (token) setSession(token, me)
      })
      .catch(() => {
        clearSession()
        setUser(null)
      })
      .finally(() => setAuthChecking(false))
  }, [])

  if (authChecking) {
    return <div className="nr-login"><p className="nr-muted">Loading…</p></div>
  }

  if (!user) {
    return (
      <LoginPage
        onLoggedIn={(nextUser, token) => {
          setSession(token, nextUser)
          setUser(nextUser)
        }}
      />
    )
  }

  return (
    <EditorApp
      user={user}
      onLogout={() => {
        clearSession()
        setUser(null)
      }}
    />
  )
}

function EditorApp({ user, onLogout }: { user: AuthUser; onLogout: () => void }) {
  const [descriptors, setDescriptors] = useState<NodeDescriptor[]>([])
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([])
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [contextMenu, setContextMenu] = useState<NodeContextMenuState>(null)
  const [providersOpen, setProvidersOpen] = useState(false)
  const [usersOpen, setUsersOpen] = useState(false)
  const [paletteCollapsed, setPaletteCollapsed] = useState(false)
  const [running, setRunning] = useState(false)
  const [saving, setSaving] = useState(false)
  const [runResult, setRunResult] = useState<PipelineRunResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [workflows, setWorkflows] = useState<WorkflowSummary[]>([])
  const [workflowId, setWorkflowId] = useState<string | null>(null)
  const [workflowName, setWorkflowName] = useState('Untitled pipeline')

  const isAdmin = user.role === 'Admin'

  const loadNodes = useCallback(async () => {
    const list = await api.getNodes()
    setDescriptors(list)
  }, [])

  const loadWorkflows = useCallback(async () => {
    setWorkflows(await api.listWorkflows())
  }, [])

  useEffect(() => {
    void loadNodes().catch((e) => setError(String(e)))
    void loadWorkflows().catch((e) => setError(String(e)))
  }, [loadNodes, loadWorkflows])

  const selectedNode = useMemo(
    () => nodes.find((n) => n.id === selectedId) ?? null,
    [nodes, selectedId],
  )

  const onConnect = useCallback(
    (connection: Connection) =>
      setEdges((eds) => addEdge({ ...connection, type: 'deletable', animated: true }, eds)),
    [setEdges],
  )

  const addNode = (descriptor: NodeDescriptor) => {
    const id = `${descriptor.providerId}-${descriptor.id}-${crypto.randomUUID().slice(0, 8)}`
    const node: Node = {
      id,
      type: 'genericNode',
      position: { x: 120 + nodes.length * 40, y: 80 + nodes.length * 50 },
      data: {
        descriptor,
        label: descriptor.name,
        params: defaultParams(descriptor),
      } satisfies GenericNodeData,
    }
    setNodes((ns) => [...ns, node])
    setSelectedId(id)
  }

  const updateParams = (nodeId: string, params: Record<string, string | number>) => {
    setNodes((ns) =>
      ns.map((n) =>
        n.id === nodeId
          ? { ...n, data: { ...(n.data as GenericNodeData), params } }
          : n,
      ),
    )
  }

  const uploadForNode = async (nodeId: string, file: File) => {
    const uploaded = await api.uploadFile(file)
    const node = nodes.find((n) => n.id === nodeId)
    if (!node) return
    const data = node.data as GenericNodeData
    updateParams(nodeId, { ...data.params, objectKey: uploaded.objectKey })
  }

  const deleteNode = useCallback((nodeId: string) => {
    setNodes((ns) => ns.filter((n) => n.id !== nodeId))
    setEdges((eds) => eds.filter((e) => e.source !== nodeId && e.target !== nodeId))
    setSelectedId((id) => (id === nodeId ? null : id))
  }, [setNodes, setEdges])

  const clipboardRef = useRef<ClipboardNode | null>(null)
  const [hasClipboard, setHasClipboard] = useState(false)
  const pasteOffsetRef = useRef(0)

  const copyNode = useCallback((nodeId: string) => {
    const source = nodes.find((n) => n.id === nodeId)
    if (!source) return
    const data = source.data as GenericNodeData
    clipboardRef.current = {
      type: source.type,
      width: source.width,
      height: source.height,
      data: {
        ...data,
        runStatus: undefined,
        runError: undefined,
        params: { ...data.params },
      },
    }
    pasteOffsetRef.current = 0
    setHasClipboard(true)
  }, [nodes])

  const pasteNode = useCallback(() => {
    const clip = clipboardRef.current
    if (!clip) return

    pasteOffsetRef.current += 1
    const step = pasteOffsetRef.current
    const base = nodes.find((n) => n.selected) ?? nodes.find((n) => n.id === selectedId)
    const origin = base?.position ?? { x: 120, y: 80 }
    const data = clip.data
    const id = `${data.descriptor.providerId}-${data.descriptor.id}-${crypto.randomUUID().slice(0, 8)}`
    const copy: Node = {
      id,
      type: clip.type ?? 'genericNode',
      position: { x: origin.x + 48 * step, y: origin.y + 48 * step },
      selected: true,
      data: {
        ...data,
        params: { ...data.params },
        runStatus: undefined,
        runError: undefined,
      } satisfies GenericNodeData,
    }
    setNodes((ns) => [...ns.map((n) => ({ ...n, selected: false })), copy])
    setSelectedId(id)
  }, [nodes, selectedId, setNodes])

  const duplicateNode = useCallback((nodeId: string) => {
    setNodes((ns) => {
      const source = ns.find((n) => n.id === nodeId)
      if (!source) return ns
      const data = source.data as GenericNodeData
      const id = `${data.descriptor.providerId}-${data.descriptor.id}-${crypto.randomUUID().slice(0, 8)}`
      const copy: Node = {
        ...source,
        id,
        position: { x: source.position.x + 40, y: source.position.y + 40 },
        selected: true,
        data: {
          ...data,
          runStatus: undefined,
          runError: undefined,
          params: { ...data.params },
        },
      }
      setSelectedId(id)
      return [...ns.map((n) => ({ ...n, selected: false })), copy]
    })
  }, [setNodes])

  const applyGraph = (graphJson: string, catalog: NodeDescriptor[]) => {
    const parsed = JSON.parse(graphJson) as GraphPayload
    const restored = (parsed.nodes ?? []).map((n) => {
      const data = n.data as GenericNodeData
      const desc =
        catalog.find(
          (d) =>
            d.id === data?.descriptor?.id &&
            d.providerId === data?.descriptor?.providerId,
        ) ?? data?.descriptor
      return {
        ...n,
        type: 'genericNode',
        data: {
          ...data,
          descriptor: desc,
          label: desc?.name ?? data?.label ?? 'Node',
          params: data?.params ?? {},
        } satisfies GenericNodeData,
      }
    })
    setNodes(restored)
    setEdges((parsed.edges ?? []).map((e) => ({ ...e, type: 'deletable' })))
    setSelectedId(null)
    setRunResult(null)
  }

  const newPipeline = () => {
    setWorkflowId(null)
    setWorkflowName('Untitled pipeline')
    setNodes([])
    setEdges([])
    setSelectedId(null)
    setRunResult(null)
    setError(null)
  }

  const savePipeline = useCallback(async () => {
    try {
      setSaving(true)
      setError(null)
      const body = {
        name: workflowName.trim() || 'Untitled pipeline',
        graphJson: JSON.stringify({ nodes, edges } satisfies GraphPayload),
      }
      if (workflowId) {
        const updated = await api.updateWorkflow(workflowId, body)
        setWorkflowId(updated.id)
        setWorkflowName(updated.name)
      } else {
        const created = await api.createWorkflow(body)
        setWorkflowId(created.id)
        setWorkflowName(created.name)
      }
      await loadWorkflows()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setSaving(false)
    }
  }, [workflowId, workflowName, nodes, edges, loadWorkflows])

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null
      const typing =
        target &&
        (target.tagName === 'INPUT' ||
          target.tagName === 'TEXTAREA' ||
          target.tagName === 'SELECT' ||
          target.isContentEditable)

      const mod = e.ctrlKey || e.metaKey
      const key = e.key.toLowerCase()

      if (mod && key === 's') {
        e.preventDefault()
        // With a node selected: copy node. Otherwise: save pipeline.
        if (!typing && selectedId) copyNode(selectedId)
        else void savePipeline()
        return
      }

      if (typing) return

      if (mod && key === 'c' && selectedId) {
        e.preventDefault()
        copyNode(selectedId)
        return
      }

      if (mod && key === 'v') {
        e.preventDefault()
        pasteNode()
        return
      }

      if (mod && key === 'd' && selectedId) {
        e.preventDefault()
        duplicateNode(selectedId)
        return
      }

      if (selectedId && (e.key === 'Delete' || e.key === 'Backspace')) {
        e.preventDefault()
        deleteNode(selectedId)
      }
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [savePipeline, selectedId, deleteNode, copyNode, pasteNode, duplicateNode])

  const openPipeline = async (id: string) => {
    try {
      setError(null)
      const wf = await api.getWorkflow(id)
      setWorkflowId(wf.id)
      setWorkflowName(wf.name)
      applyGraph(wf.graphJson, descriptors)
    } catch (e) {
      setError(String(e))
    }
  }

  const deletePipeline = async () => {
    if (!workflowId) return
    if (!confirm(`Delete pipeline “${workflowName}”?`)) return
    try {
      await api.deleteWorkflow(workflowId)
      await loadWorkflows()
      newPipeline()
    } catch (e) {
      setError(String(e))
    }
  }

  const run = async () => {
    try {
      setRunning(true)
      setError(null)
      setRunResult(null)
      clearRunVisuals()

      // Optimistic: mark first source-ish node as running immediately for feedback.
      const firstId = nodes[0]?.id
      if (firstId) {
        setNodes((ns) =>
          ns.map((n) =>
            n.id === firstId
              ? { ...n, data: { ...(n.data as GenericNodeData), runStatus: 'Running', runError: undefined } }
              : n,
          ),
        )
      }

      const payload = {
        nodes: nodes.map((n) => {
          const data = n.data as GenericNodeData
          return {
            id: n.id,
            type: data.descriptor.id,
            providerId: data.descriptor.providerId,
            params: data.params,
            data: {
              nodeTypeId: data.descriptor.id,
              providerId: data.descriptor.providerId,
              params: data.params,
            },
          }
        }),
        edges: edges.map((e) => ({
          id: e.id,
          source: e.source,
          target: e.target,
          sourceHandle: e.sourceHandle,
          targetHandle: e.targetHandle,
        })),
      }

      const started = await api.runPipeline(payload)
      setRunResult(started)
      applyRunVisuals(started)

      let current = started
      const startedAt = Date.now()
      const maxMs = 3 * 60 * 1000

      while (normalizeStatus(current.status) === 'Pending' || normalizeStatus(current.status) === 'Running') {
        if (Date.now() - startedAt > maxMs) {
          throw new Error('Pipeline timed out after 3 minutes. Check that FFmpeg is installed and the API is running.')
        }
        await new Promise((r) => setTimeout(r, 250))
        current = await api.getRun(started.id)
        setRunResult(current)
        applyRunVisuals(current)
      }

      if (normalizeStatus(current.status) === 'Failed') {
        setError(current.error || 'Pipeline failed')
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setRunning(false)
    }
  }

  const normalizeStatus = (status: PipelineRunResult['status'] | string | number) => {
    if (typeof status === 'number') {
      return ['Pending', 'Running', 'Succeeded', 'Failed', 'Skipped'][status] ?? String(status)
    }
    return String(status)
  }

  const clearRunVisuals = () => {
    setNodes((ns) =>
      ns.map((n) => ({
        ...n,
        data: { ...(n.data as GenericNodeData), runStatus: undefined, runError: undefined },
      })),
    )
    setEdges((eds) =>
      eds.map((e) => ({
        ...e,
        className: undefined,
        style: { ...e.style, stroke: undefined },
        animated: true,
      })),
    )
  }

  const applyRunVisuals = (result: PipelineRunResult) => {
    const byNode = new Map(result.steps.map((s) => [s.nodeInstanceId, s]))

    setNodes((ns) =>
      ns.map((n) => {
        const step = byNode.get(n.id)
        const status = step ? normalizeStatus(step.status) : undefined
        return {
          ...n,
          data: {
            ...(n.data as GenericNodeData),
            runStatus: status,
            runError: step?.error,
          },
        }
      }),
    )

    setEdges((eds) =>
      eds.map((e) => {
        const sourceStep = byNode.get(e.source)
        const targetStep = byNode.get(e.target)
        if (!sourceStep || normalizeStatus(sourceStep.status) !== 'Succeeded') {
          return { ...e, className: undefined, style: { ...e.style, stroke: undefined }, animated: true }
        }

        const outputs = sourceStep.outputs ?? {}
        const handle = e.sourceHandle || 'video'
        const taken = Object.prototype.hasOwnProperty.call(outputs, handle) ||
          (Object.keys(outputs).length === 1 && !e.sourceHandle)

        if (!taken) {
          return { ...e, className: 'nr-edge-idle', style: { stroke: '#3a4558' }, animated: false }
        }

        const targetStatus = targetStep ? normalizeStatus(targetStep.status) : undefined
        const active =
          targetStatus === 'Running' ||
          targetStatus === 'Succeeded' ||
          targetStatus === 'Failed'

        return {
          ...e,
          className: active ? 'nr-edge-active' : 'nr-edge-done',
          style: { stroke: targetStatus === 'Failed' ? '#ef6b6b' : '#3ecf8e', strokeWidth: 2 },
          animated: targetStatus === 'Running',
        }
      }),
    )
  }

  const statusLabel = (status: PipelineRunResult['status']) => normalizeStatus(status)

  return (
    <div className={`nr-app ${paletteCollapsed ? 'palette-collapsed' : ''}`}>
      <header className="nr-topbar">
        <div className="nr-brand">
          <span className="nr-logo">NodeReel</span>
          <div className="nr-workflow-bar">
            <select
              value={workflowId ?? ''}
              onChange={(e) => {
                const id = e.target.value
                if (!id) newPipeline()
                else void openPipeline(id)
              }}
            >
              <option value="">New pipeline…</option>
              {workflows.map((w) => (
                <option key={w.id} value={w.id}>{w.name}</option>
              ))}
            </select>
            <input
              className="nr-workflow-name"
              value={workflowName}
              onChange={(e) => setWorkflowName(e.target.value)}
              placeholder="Pipeline name"
            />
          </div>
        </div>
        <div className="nr-actions">
          <span className="nr-user-chip" title={user.role}>{user.username}</span>
          {isAdmin && (
            <button type="button" onClick={() => setUsersOpen(true)}>Users</button>
          )}
          <button type="button" onClick={newPipeline}>New</button>
          <button
            type="button"
            disabled={saving}
            onClick={() => void savePipeline()}
            title={selectedId ? 'Save pipeline (Ctrl+S copies selected node — click empty canvas first)' : 'Ctrl+S'}
          >
            {saving ? 'Saving…' : 'Save'}
          </button>
          <button type="button" disabled={!workflowId} onClick={() => void deletePipeline()}>Delete</button>
          {isAdmin && (
            <button type="button" onClick={() => setProvidersOpen(true)}>Providers</button>
          )}
          <button type="button" onClick={() => void loadNodes()}>Refresh nodes</button>
          <button
            type="button"
            className="primary nr-play-btn"
            disabled={running || nodes.length === 0}
            onClick={() => void run()}
            title="Run pipeline"
          >
            <span className="nr-play-icon" aria-hidden />
            {running ? 'Running…' : 'Run'}
          </button>
          <button type="button" onClick={onLogout}>Log out</button>
        </div>
      </header>

      <div className="nr-main">
        <NodePalette
          nodes={descriptors}
          collapsed={paletteCollapsed}
          onToggle={() => setPaletteCollapsed((v) => !v)}
          onAdd={addNode}
        />

        <div className="nr-canvas">
          <ReactFlow
            nodes={nodes}
            edges={edges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={onConnect}
            nodeTypes={nodeTypes}
            edgeTypes={edgeTypes}
            defaultEdgeOptions={{ type: 'deletable', animated: true }}
            connectionRadius={36}
            deleteKeyCode={['Backspace', 'Delete']}
            onNodesDelete={(deleted) => {
              const ids = new Set(deleted.map((n) => n.id))
              setEdges((eds) => eds.filter((e) => !ids.has(e.source) && !ids.has(e.target)))
              setSelectedId((id) => (id && ids.has(id) ? null : id))
            }}
            onSelectionChange={({ nodes: selected }) => setSelectedId(selected[0]?.id ?? null)}
            onNodeContextMenu={(event, node) => {
              event.preventDefault()
              setSelectedId(node.id)
              setNodes((ns) => ns.map((n) => ({ ...n, selected: n.id === node.id })))
              setContextMenu({ nodeId: node.id, x: event.clientX, y: event.clientY })
            }}
            onPaneClick={() => setContextMenu(null)}
            onMoveStart={() => setContextMenu(null)}
            fitView
          >
            <Background gap={18} size={1} color="#2a3344" />
            <Controls />
            <MiniMap
              pannable
              zoomable
              bgColor="#151c28"
              maskColor="rgba(14, 20, 29, 0.65)"
              nodeColor="#3a4a63"
            />
          </ReactFlow>
          <NodeContextMenu
            menu={contextMenu}
            onClose={() => setContextMenu(null)}
            onCopy={copyNode}
            onDuplicate={duplicateNode}
            onDelete={deleteNode}
            onPaste={pasteNode}
            canPaste={hasClipboard}
          />
        </div>

        <ParamsPanel
          node={selectedNode}
          running={running}
          runResult={runResult}
          onChange={updateParams}
          onUpload={uploadForNode}
          onDelete={deleteNode}
          onDuplicate={duplicateNode}
          onRun={() => void run()}
        />
      </div>

      {(error || runResult) && (
        <footer className="nr-status">
          {error && <span className="nr-error">{error}</span>}
          {runResult && (
            <span>
              Run {runResult.id.slice(0, 8)}… — {statusLabel(runResult.status)}
              {runResult.resultObjectKey && (
                <>
                  {' '}
                  <a href={api.downloadUrl(runResult.resultObjectKey, true)} download="result.mp4">
                    Download result
                  </a>
                </>
              )}
            </span>
          )}
        </footer>
      )}

      <ProvidersModal
        open={providersOpen}
        onClose={() => setProvidersOpen(false)}
        onChanged={() => void loadNodes()}
      />
      <UsersModal open={usersOpen} onClose={() => setUsersOpen(false)} />
    </div>
  )
}
