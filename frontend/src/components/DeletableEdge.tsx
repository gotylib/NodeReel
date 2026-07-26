import {
  BaseEdge,
  EdgeLabelRenderer,
  getBezierPath,
  useReactFlow,
  type EdgeProps,
} from '@xyflow/react'
import { useRef, useState } from 'react'

export function DeletableEdge({
  id,
  sourceX,
  sourceY,
  targetX,
  targetY,
  sourcePosition,
  targetPosition,
  style,
  markerEnd,
  selected,
  className,
}: EdgeProps & { className?: string }) {
  const { deleteElements } = useReactFlow()
  const [hovered, setHovered] = useState(false)
  const leaveTimer = useRef<number | null>(null)
  const [edgePath, labelX, labelY] = getBezierPath({
    sourceX,
    sourceY,
    targetX,
    targetY,
    sourcePosition,
    targetPosition,
  })

  const enter = () => {
    if (leaveTimer.current != null) {
      window.clearTimeout(leaveTimer.current)
      leaveTimer.current = null
    }
    setHovered(true)
  }

  const leave = () => {
    leaveTimer.current = window.setTimeout(() => setHovered(false), 140)
  }

  const showDelete = hovered || selected

  return (
    <>
      <g onMouseEnter={enter} onMouseLeave={leave}>
        <BaseEdge
          id={id}
          path={edgePath}
          markerEnd={markerEnd}
          className={className}
          interactionWidth={32}
          style={{
            ...style,
            strokeWidth: hovered || selected ? 2.6 : Number(style?.strokeWidth) || 2,
          }}
        />
      </g>
      {showDelete && (
        <EdgeLabelRenderer>
          <button
            type="button"
            className="nr-edge-delete nodrag nopan"
            style={{
              position: 'absolute',
              transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)`,
              pointerEvents: 'all',
            }}
            title="Delete connection"
            aria-label="Delete connection"
            onMouseEnter={enter}
            onMouseLeave={leave}
            onClick={(e) => {
              e.stopPropagation()
              void deleteElements({ edges: [{ id }] })
            }}
          >
            <svg viewBox="0 0 12 12" width="10" height="10" aria-hidden>
              <path
                d="M2.5 2.5l7 7M9.5 2.5l-7 7"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.6"
                strokeLinecap="round"
              />
            </svg>
          </button>
        </EdgeLabelRenderer>
      )}
    </>
  )
}
