import type { ReactNode } from 'react'
import type { NodeDescriptor } from '../api'

const icons: Record<string, ReactNode> = {
  upload: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M12 16V6M12 6l-4 4M12 6l4 4" />
      <path d="M5 18h14" />
    </svg>
  ),
  strip: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <rect x="4" y="5" width="16" height="14" rx="2" />
      <path d="M8 9h8M8 12h5M8 15h6" />
      <path d="M16 14l3 3M19 14l-3 3" />
    </svg>
  ),
  noise: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <rect x="3" y="5" width="18" height="14" rx="2" />
      <circle cx="8" cy="12" r="1.2" fill="currentColor" stroke="none" />
      <circle cx="12" cy="10" r="1" fill="currentColor" stroke="none" />
      <circle cx="15" cy="13" r="1.3" fill="currentColor" stroke="none" />
      <circle cx="10" cy="15" r="0.9" fill="currentColor" stroke="none" />
    </svg>
  ),
  echo: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M8 8h8v8H8z" />
      <path d="M11 5h8v8" />
    </svg>
  ),
  video: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <rect x="3" y="6" width="13" height="12" rx="2" />
      <path d="M16 10l5-3v10l-5-3z" />
    </svg>
  ),
  image: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <rect x="3" y="5" width="18" height="14" rx="2" />
      <circle cx="8.5" cy="10" r="1.5" />
      <path d="M5 17l4.5-4.5L13 16l2.5-2.5L19 17" />
    </svg>
  ),
  trim: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M6 4v16M18 4v16" />
      <path d="M6 12h12" />
      <path d="M9 8l-3 4 3 4M15 8l3 4-3 4" />
    </svg>
  ),
  audio: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M9 18V6l10-2v12" />
      <circle cx="7" cy="18" r="2.5" />
      <circle cx="17" cy="16" r="2.5" />
    </svg>
  ),
  mute: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M4 10v4h3l5 4V6L7 10H4z" />
      <path d="M16 9l5 5M21 9l-5 5" />
    </svg>
  ),
  speed: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <circle cx="12" cy="12" r="8" />
      <path d="M12 12l4-2" />
      <path d="M12 8v1" />
    </svg>
  ),
  resize: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M4 10V4h6M20 14v6h-6" />
      <path d="M4 4l7 7M20 20l-7-7" />
    </svg>
  ),
  frame: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <rect x="4" y="5" width="16" height="14" rx="2" />
      <path d="M4 15h16" />
      <circle cx="12" cy="10" r="2" />
    </svg>
  ),
  rotate: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M20 12a8 8 0 1 1-2.3-5.7" />
      <path d="M20 4v5h-5" />
    </svg>
  ),
  volume: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M4 10v4h3l5 4V6L7 10H4z" />
      <path d="M16 9a4 4 0 0 1 0 6" />
      <path d="M18.5 7a7 7 0 0 1 0 10" />
    </svg>
  ),
  flip: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M12 4v16" />
      <path d="M8 8H4l4 4-4 4h4" />
      <path d="M16 8h4l-4 4 4 4h-4" />
    </svg>
  ),
  reverse: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M8 7v10l-4-5 4-5z" />
      <path d="M12 7v10l8-5-8-5z" />
    </svg>
  ),
  fade: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M4 18V6" />
      <path d="M8 18V9" />
      <path d="M12 18v-5" />
      <path d="M16 18v-2" />
      <path d="M20 18v-1" />
    </svg>
  ),
  grayscale: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <circle cx="12" cy="12" r="8" />
      <path d="M12 4v16" />
      <path d="M12 4a8 8 0 0 1 0 16" fill="currentColor" opacity="0.35" stroke="none" />
    </svg>
  ),
  crop: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M6 3v15h15" />
      <path d="M3 6h15v15" />
    </svg>
  ),
  blur: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <circle cx="8" cy="12" r="3" opacity="0.4" />
      <circle cx="12" cy="12" r="3" opacity="0.7" />
      <circle cx="16" cy="12" r="3" />
    </svg>
  ),
  imageVideo: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <rect x="3" y="5" width="11" height="10" rx="1.5" />
      <path d="M14 10l6-3v10l-6-3z" />
    </svg>
  ),
  default: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <rect x="4" y="4" width="16" height="16" rx="3" />
      <path d="M8 12h8M12 8v8" />
    </svg>
  ),
  if: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M12 4v6M12 14v6" />
      <path d="M12 10l-5 4M12 10l5 4" />
      <circle cx="12" cy="12" r="2" />
    </svg>
  ),
  switch: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M4 12h6" />
      <path d="M10 12l4-5h6" />
      <path d="M10 12l4 5h6" />
    </svg>
  ),
  merge: (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M4 7h6l4 5-4 5H4" />
      <path d="M14 12h6" />
    </svg>
  ),
}

export function resolveNodeIcon(descriptor: NodeDescriptor): string {
  if (descriptor.icon) return descriptor.icon
  if (descriptor.id.includes('upload')) return 'upload'
  if (descriptor.id.includes('strip') || descriptor.id.includes('meta')) return 'strip'
  if (descriptor.id.includes('noise')) return 'noise'
  if (descriptor.id.includes('echo')) return 'echo'
  if (descriptor.id === 'if' || descriptor.id.includes('if')) return 'if'
  if (descriptor.id.includes('switch')) return 'switch'
  if (descriptor.id.includes('merge')) return 'merge'
  if (descriptor.category === 'image') return 'image'
  if (descriptor.category === 'audio') return 'audio'
  if (descriptor.category === 'video' || descriptor.category === 'input') return 'video'
  if (descriptor.category === 'flow') return 'switch'
  return 'default'
}

export function NodeIcon({ name, className }: { name: string; className?: string }) {
  return <span className={className ?? 'nr-node-icon'}>{icons[name] ?? icons.default}</span>
}
