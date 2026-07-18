import { useEffect, useRef } from 'react'

export type NodeContextMenuState = {
  nodeId: string
  x: number
  y: number
} | null

type Props = {
  menu: NodeContextMenuState
  onClose: () => void
  onCopy: (nodeId: string) => void
  onDuplicate: (nodeId: string) => void
  onDelete: (nodeId: string) => void
  onPaste: () => void
  canPaste: boolean
}

export function NodeContextMenu({
  menu,
  onClose,
  onCopy,
  onDuplicate,
  onDelete,
  onPaste,
  canPaste,
}: Props) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!menu) return

    const onPointerDown = (e: PointerEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose()
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    const onScroll = () => onClose()

    window.addEventListener('pointerdown', onPointerDown, true)
    window.addEventListener('keydown', onKey)
    window.addEventListener('scroll', onScroll, true)
    return () => {
      window.removeEventListener('pointerdown', onPointerDown, true)
      window.removeEventListener('keydown', onKey)
      window.removeEventListener('scroll', onScroll, true)
    }
  }, [menu, onClose])

  if (!menu) return null

  const run = (action: () => void) => {
    action()
    onClose()
  }

  return (
    <div
      ref={ref}
      className="nr-context-menu"
      style={{ left: menu.x, top: menu.y }}
      role="menu"
    >
      <button type="button" role="menuitem" onClick={() => run(() => onCopy(menu.nodeId))}>
        <span>Copy</span>
        <kbd>Ctrl+S</kbd>
      </button>
      <button type="button" role="menuitem" onClick={() => run(() => onDuplicate(menu.nodeId))}>
        <span>Duplicate</span>
        <kbd>Ctrl+D</kbd>
      </button>
      <button
        type="button"
        role="menuitem"
        disabled={!canPaste}
        onClick={() => run(onPaste)}
      >
        <span>Paste</span>
        <kbd>Ctrl+V</kbd>
      </button>
      <div className="nr-context-sep" />
      <button
        type="button"
        role="menuitem"
        className="danger"
        onClick={() => run(() => onDelete(menu.nodeId))}
      >
        <span>Delete</span>
        <kbd>Del</kbd>
      </button>
    </div>
  )
}
