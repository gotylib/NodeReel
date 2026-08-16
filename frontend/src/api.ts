export type NodePort = {
  name: string
  type: string
  required: boolean
}

export type NodeDescriptor = {
  id: string
  providerId: string
  name: string
  category: string
  description: string
  icon?: string
  subtitle?: string
  inputs: NodePort[]
  outputs: NodePort[]
  paramsSchema: {
    type?: string
    properties?: Record<string, {
      type?: string
      minimum?: number
      maximum?: number
      default?: number | string
      description?: string
      enum?: string[]
    }>
    required?: string[]
  }
}

export type NodeProvider = {
  id: string
  name: string
  baseUrl: string
  isEnabled: boolean
  createdAt: string
}

export type FileUploadResult = {
  objectKey: string
  contentType: string
  originalFileName?: string
  sizeBytes: number
}

export type PipelineRunResult = {
  id: string
  status: 'Pending' | 'Running' | 'Succeeded' | 'Failed' | 'Skipped' | number
  error?: string
  resultObjectKey?: string
  createdAt: string
  finishedAt?: string
  steps: Array<{
    nodeInstanceId: string
    nodeTypeId: string
    providerId: string
    status: string | number
    inputs?: Record<string, string>
    outputs?: Record<string, string>
    error?: string
  }>
}

export type WorkflowSummary = {
  id: string
  name: string
  createdAt: string
  updatedAt: string
}

export type Workflow = WorkflowSummary & {
  graphJson: string
}

export type AuthUser = {
  id: string
  username: string
  role: 'Admin' | 'User' | string
  createdAt: string
}

export type LoginResult = {
  token: string
  user: AuthUser
}

const TOKEN_KEY = 'nodereel_token'
const USER_KEY = 'nodereel_user'

export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function getStoredUser(): AuthUser | null {
  const raw = localStorage.getItem(USER_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as AuthUser
  } catch {
    return null
  }
}

export function setSession(token: string, user: AuthUser) {
  localStorage.setItem(TOKEN_KEY, token)
  localStorage.setItem(USER_KEY, JSON.stringify(user))
}

export function clearSession() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(USER_KEY)
}

async function request<T>(url: string, init?: RequestInit, auth = true): Promise<T> {
  const headers = new Headers(init?.headers)
  if (auth) {
    const token = getStoredToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(url, { ...init, headers })
  if (response.status === 401) {
    clearSession()
    if (!url.includes('/api/auth/login')) {
      window.dispatchEvent(new Event('nr:unauthorized'))
    }
  }
  if (!response.ok) {
    const text = await response.text()
    try {
      const body = JSON.parse(text) as { message?: string; error?: string }
      throw new Error(body.message || body.error || text || `${response.status}`)
    } catch (e) {
      if (e instanceof Error && e.message && !e.message.startsWith('Unexpected')) throw e
      throw new Error(text.slice(0, 300) || `${response.status} ${response.statusText}`)
    }
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const api = {
  login: (username: string, password: string) =>
    request<LoginResult>('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    }, false),
  me: () => request<AuthUser>('/api/auth/me'),
  listUsers: () => request<AuthUser[]>('/api/users'),
  createUser: (body: { username: string; password: string; role?: string }) =>
    request<AuthUser>('/api/users', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }),
  changeUserPassword: (id: string, newPassword: string) =>
    request<void>(`/api/users/${id}/password`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ newPassword }),
    }),
  deleteUser: (id: string) => request<void>(`/api/users/${id}`, { method: 'DELETE' }),
  getNodes: () => request<NodeDescriptor[]>('/api/nodes'),
  refreshNodes: () => request<void>('/api/nodes/refresh', { method: 'POST' }),
  getProviders: () => request<NodeProvider[]>('/api/providers'),
  createProvider: (body: { name: string; baseUrl: string }) =>
    request<NodeProvider>('/api/providers', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }),
  deleteProvider: (id: string) =>
    request<void>(`/api/providers/${id}`, { method: 'DELETE' }),
  uploadFile: async (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<FileUploadResult>('/api/files', { method: 'POST', body: form })
  },
  runPipeline: (body: unknown) =>
    request<PipelineRunResult>('/api/pipelines/run', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }),
  getRun: (id: string) => request<PipelineRunResult>(`/api/pipelines/runs/${id}`),
  listWorkflows: () => request<WorkflowSummary[]>('/api/workflows'),
  getWorkflow: (id: string) => request<Workflow>(`/api/workflows/${id}`),
  createWorkflow: (body: { name: string; graphJson: string }) =>
    request<Workflow>('/api/workflows', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }),
  updateWorkflow: (id: string, body: { name: string; graphJson: string }) =>
    request<Workflow>(`/api/workflows/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }),
  deleteWorkflow: (id: string) =>
    request<void>(`/api/workflows/${id}`, { method: 'DELETE' }),
  downloadUrl: (objectKey: string, asAttachment = false) => {
    const token = getStoredToken()
    const params = new URLSearchParams({ key: objectKey })
    if (asAttachment) params.set('download', 'true')
    if (token) params.set('access_token', token)
    return `/api/files/by-key?${params.toString()}`
  },
}
