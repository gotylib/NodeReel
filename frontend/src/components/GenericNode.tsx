import { Handle, Position, type NodeProps } from '@xyflow/react'
import type { NodeDescriptor, NodePort } from '../api'
import { NodeIcon, resolveNodeIcon } from './NodeIcon'

export type GenericNodeData = {
  descriptor: NodeDescriptor
  params: Record<string, string | number>
  label: string
  runStatus?: string
  runError?: string
}

function statusClass(status?: string): string {
  if (!status) return ''
  return `status-${status.toLowerCase()}`
}

function portTypeClass(type: string): string {
  const t = type.toLowerCase()
  if (t.includes('video')) return 'type-video'
  if (t.includes('audio')) return 'type-audio'
  if (t.includes('image')) return 'type-image'
  return 'type-default'
}

export function GenericNode({ data, selected }: NodeProps) {
  const nodeData = data as GenericNodeData
  const { descriptor, label, runStatus, runError } = nodeData
  const icon = resolveNodeIcon(descriptor)
  const subtitle = descriptor.subtitle || descriptor.category || descriptor.providerId
  const isRunning = runStatus === 'Running'
  const rowCount = Math.max(descriptor.inputs.length, descriptor.outputs.length, 0)

  return (
    <div
      className={`nr-node ${selected ? 'selected' : ''} ${statusClass(runStatus)}`}
      title={runError || descriptor.description}
    >
      <div className="nr-node-main">
        <NodeIcon name={icon} />
        <div className="nr-node-text">
          <div className="nr-node-title">{label}</div>
          <div className="nr-node-sub">{isRunning ? 'processing…' : subtitle}</div>
        </div>
        {runStatus && (
          <span className="nr-node-badge">
            {isRunning && <span className="nr-node-spinner" aria-hidden />}
            {runStatus}
          </span>
        )}
      </div>

      {runStatus === 'Failed' && runError && (
        <div className="nr-node-error-msg">{runError}</div>
      )}

      {rowCount > 0 && (
        <div className="nr-node-ports">
          {Array.from({ length: rowCount }, (_, index) => {
            const input = descriptor.inputs[index] as NodePort | undefined
            const output = descriptor.outputs[index] as NodePort | undefined
            return (
              <div key={`row-${index}`} className="nr-port-row">
                {input && (
                  <Handle
                    type="target"
                    position={Position.Left}
                    id={input.name}
                    className={`nr-handle ${portTypeClass(input.type)}`}
                    title={`${input.name} (${input.type})`}
                  />
                )}
                <span className={`nr-port-caption in ${input ? '' : 'empty'}`}>
                  {input?.name ?? ''}
                </span>
                <span className={`nr-port-caption out ${output ? '' : 'empty'}`}>
                  {output?.name ?? ''}
                </span>
                {output && (
                  <Handle
                    type="source"
                    position={Position.Right}
                    id={output.name}
                    className={`nr-handle ${portTypeClass(output.type)}`}
                    title={`${output.name} (${output.type})`}
                  />
                )}
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
