import { apiBlob, apiRequest } from './client'

export interface AttachmentResponse {
  id: string
  name: string
  contentType: string
  contentLength: number
  storagePath: string
  hash: string | null
  createdAt: string
}

export const attachmentsApi = {
  upload: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return apiRequest<AttachmentResponse>('/api/attachments', { method: 'POST', body: form })
  },

  delete: (id: string) => apiRequest<void>(`/api/attachments/${id}`, { method: 'DELETE' }),

  content: (id: string) => apiBlob(`/api/attachments/${id}/content`),

  forNote: (noteId: string) => apiRequest<AttachmentResponse[]>(`/api/notes/${noteId}/attachments`),

  attachToNote: (noteId: string, attachmentId: string) =>
    apiRequest<void>(`/api/notes/${noteId}/attachments/${attachmentId}`, { method: 'PUT' }),
}
