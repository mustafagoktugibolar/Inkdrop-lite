import { apiRequest } from './client'

export enum NoteStatus {
  None = 'None',
  Active = 'Active',
  OnHold = 'OnHold',
  Completed = 'Completed',
  Dropped = 'Dropped',
}

export interface NoteResponse {
  id: string
  title: string
  content: string
  status: NoteStatus
  pinned: boolean
  notebookId: string
  sourceTemplateId: string | null
  tagIds: string[]
  createdAt: string
  updatedAt: string
  /** 'app' for the web UI, 'mcp:{client}/{version}' for agents. */
  createdSource: string
  updatedSource: string
}

export interface SaveNoteRequest {
  title: string
  content: string
  status: NoteStatus
  pinned: boolean
  notebookId: string
  tagIds: string[]
  /** Honoured on create only. */
  sourceTemplateId?: string | null
}

export interface NoteSummaryResponse {
  id: string
  title: string
  status: NoteStatus
  pinned: boolean
  notebookId: string
  contentLength: number
  tagIds: string[]
  updatedAt: string
}

export interface NoteSearchParams {
  query?: string
  notebookId?: string
  tagId?: string
  status?: NoteStatus
  limit?: number
}

export const notesApi = {
  /** Server-side search: matches title, content, notebook name and tag name. */
  search: (params: NoteSearchParams) => {
    const search = new URLSearchParams()
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== '') search.set(key, String(value))
    }
    return apiRequest<NoteSummaryResponse[]>(`/api/notes/search?${search}`)
  },

  getAll: () => apiRequest<NoteResponse[]>('/api/notes'),

  getById: (id: string) => apiRequest<NoteResponse>(`/api/notes/${id}`),

  create: (request: SaveNoteRequest) =>
    apiRequest<NoteResponse>('/api/notes', {
      method: 'POST',
      body: JSON.stringify(request),
    }),

  update: (id: string, request: SaveNoteRequest) =>
    apiRequest<void>(`/api/notes/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
    }),

  delete: (id: string) =>
    apiRequest<void>(`/api/notes/${id}`, {
      method: 'DELETE',
    }),
}
