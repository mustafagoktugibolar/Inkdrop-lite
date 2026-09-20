import { apiRequest } from './client'

export type NotebookIconType = 'None' | 'Svg' | 'Attachment'

export interface NotebookResponse {
  id: string
  name: string
  parentNotebookId: string | null
  order: number | null
  iconType: NotebookIconType
  iconSvg: string | null
  iconAttachmentId: string | null
  createdAt: string
  updatedAt: string
}

export interface SaveNotebookRequest {
  name: string
  parentNotebookId: string | null
  order: number | null
  iconType: NotebookIconType
  iconSvg: string | null
  iconAttachmentId: string | null
}

export const notebooksApi = {
  getAll: () => apiRequest<NotebookResponse[]>('/api/notebooks'),

  getById: (id: string) => apiRequest<NotebookResponse>(`/api/notebooks/${id}`),

  create: (request: SaveNotebookRequest) =>
    apiRequest<NotebookResponse>('/api/notebooks', {
      method: 'POST',
      body: JSON.stringify(request),
    }),

  update: (id: string, request: SaveNotebookRequest) =>
    apiRequest<void>(`/api/notebooks/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
    }),

  delete: (id: string) =>
    apiRequest<void>(`/api/notebooks/${id}`, {
      method: 'DELETE',
    }),
}
