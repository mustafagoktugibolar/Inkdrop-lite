import { getAccessToken } from '@/auth/entra'

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL || '').replace(/\/$/, '')

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem?: ProblemDetails,
  ) {
    super(problem?.detail || problem?.title || `API request failed with status ${status}.`)
  }
}

async function send(path: string, init: RequestInit, accept: string): Promise<Response> {
  const token = await getAccessToken()
  const headers = new Headers(init.headers)

  headers.set('Accept', accept)
  headers.set('Authorization', `Bearer ${token}`)

  if (init.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers,
  })

  if (!response.ok) {
    let problem: ProblemDetails | undefined

    if (response.headers.get('content-type')?.includes('application/problem+json')) {
      problem = (await response.json()) as ProblemDetails
    }

    throw new ApiError(response.status, problem)
  }

  return response
}

export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await send(path, init, 'application/json')

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

/** Authenticated download (a plain <a>/<img> cannot send the bearer token). */
export async function apiBlob(path: string): Promise<Blob> {
  return (await send(path, {}, '*/*')).blob()
}

export async function checkApiHealth(): Promise<boolean> {
  const response = await fetch(`${apiBaseUrl}/health/live`)
  return response.ok
}
