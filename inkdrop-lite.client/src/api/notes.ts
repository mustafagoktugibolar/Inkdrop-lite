import { apiRequest } from './client'

export enum NoteStatus {
  Active,
  OnHold,
  Completed,
  Dropped,
}

export interface NoteResponse {
  id: string
  title: string
  content: string
  status: NoteStatus
  notebookId: string | null
  tagIds: string[]
  createdAt: string
  updatedAt: string
}

export interface SaveNoteRequest {
  title: string
  content: string
  status: NoteStatus
  notebookId: string | null
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
