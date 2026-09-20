import { apiRequest } from './client'

export type TagColor =
  | 'Default' | 'Red' | 'Orange' | 'Yellow' | 'Olive' | 'Green' | 'Teal'
  | 'Blue' | 'Violet' | 'Purple' | 'Pink' | 'Brown' | 'Grey' | 'Black'

export interface TagResponse {
  id: string
  name: string
  color: TagColor
  createdAt: string
  updatedAt: string
}

export interface SaveTagRequest {
  name: string
  color: TagColor
}

export const tagsApi = {
  getAll: () => apiRequest<TagResponse[]>('/api/tags'),

  getById: (id: string) => apiRequest<TagResponse>(`/api/tags/${id}`),

  create: (request: SaveTagRequest) =>
    apiRequest<TagResponse>('/api/tags', {
      method: 'POST',
      body: JSON.stringify(request),
    }),

  update: (id: string, request: SaveTagRequest) =>
    apiRequest<void>(`/api/tags/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
    }),

  delete: (id: string) =>
    apiRequest<void>(`/api/tags/${id}`, {
      method: 'DELETE',
    }),
}
