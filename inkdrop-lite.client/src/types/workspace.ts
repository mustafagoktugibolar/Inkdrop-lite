import type { NoteStatus } from '@/api/notes'

export type NavFilter =
  | { kind: 'all'; label: string }
  | { kind: 'notebook'; id: string; label: string }
  | { kind: 'status'; id: NoteStatus; label: string }
  | { kind: 'tag'; id: string; label: string }

export type MobilePane = 'library' | 'notes' | 'editor'
export type Theme = 'light' | 'dark'
