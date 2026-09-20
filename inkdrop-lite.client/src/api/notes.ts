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
}

export const notesApi = {
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
