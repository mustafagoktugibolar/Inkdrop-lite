<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { HugeiconsIcon } from '@hugeicons/vue'
import { ArrowLeft01Icon, Attachment01Icon, Copy01Icon, Download01Icon, Delete02Icon, Edit02Icon, Layout2ColumnIcon, Note01Icon, NoteAddIcon, PinIcon, PinOffIcon, ViewIcon } from '@hugeicons/core-free-icons'

import type { AttachmentResponse } from '@/api/attachments'
import type { NotebookResponse } from '@/api/notebooks'
import { NoteStatus, type NoteResponse } from '@/api/notes'
import type { TagResponse } from '@/api/tags'
import type { MobilePane } from '@/types/workspace'
import { cn } from '@/lib/utils'
import { sourceLabel } from '@/lib/noteSource'
import { renderMarkdown } from '@/lib/markdown'
import { notebookPath } from '@/lib/notebookTree'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { Field, FieldGroup, FieldLabel, FieldLegend, FieldSet } from '@/components/ui/field'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'

const props = defineProps<{
  attachments: AttachmentResponse[]
  busy: boolean
  fresh: boolean
  mobilePane: MobilePane
  notebooks: NotebookResponse[]
  note: NoteResponse | null
  tags: TagResponse[]
  templateName: string | null
}>()

const content = defineModel<string>('content', { required: true })
const status = defineModel<NoteStatus>('status', { required: true })
const pinned = defineModel<boolean>('pinned', { required: true })
const notebookId = defineModel<string>('notebookId', { required: true })
const tagIds = defineModel<string[]>('tagIds', { required: true })

const emit = defineEmits<{
  back: []
  create: []
  delete: [note: NoteResponse]
  deleteAttachment: [attachment: AttachmentResponse]
  downloadAttachment: [attachment: AttachmentResponse]
  save: []
  uploadAttachments: [files: File[]]
  useAsTemplate: [note: NoteResponse]
}>()

const noteStatuses = [
  { value: NoteStatus.Active, label: 'Active' },
  { value: NoteStatus.OnHold, label: 'On Hold' },
  { value: NoteStatus.Completed, label: 'Completed' },
  { value: NoteStatus.Dropped, label: 'Dropped' },
]
const statusModel = computed({
  get: () => status.value,
  set: (value: string) => { status.value = value as NoteStatus },
})
const notebookModel = computed({
  get: () => notebookId.value,
  set: (value: string) => { notebookId.value = value },
})
const selectedNotebookName = computed(() => notebookId.value ? notebookPath(props.notebooks, notebookId.value) || 'No notebook' : 'No notebook')
const selectedTags = computed(() => props.tags.filter((tag) => tagIds.value.includes(tag.id)))
const wordCount = computed(() => content.value.trim() ? content.value.trim().split(/\s+/).length : 0)

type ViewMode = 'edit' | 'split' | 'preview'

function storedViewMode(): ViewMode {
  try {
    const value = localStorage.getItem('inkdrop-lite-view-mode')
    return value === 'split' || value === 'preview' ? value : 'edit'
  } catch { return 'edit' }
}

const viewMode = ref<ViewMode>(storedViewMode())
const viewModes = [
  { value: 'edit' as const, label: 'Edit', icon: Edit02Icon },
  { value: 'split' as const, label: 'Edit and preview side by side', icon: Layout2ColumnIcon },
  { value: 'preview' as const, label: 'Preview', icon: ViewIcon },
]
// Only render while a preview is visible; a note can be up to 1 MiB.
const previewHtml = computed(() => viewMode.value === 'edit' ? '' : renderMarkdown(content.value))

watch(viewMode, (value) => { try { localStorage.setItem('inkdrop-lite-view-mode', value) } catch { /* storage unavailable */ } })

const fileInput = ref<HTMLInputElement | null>(null)

function onFilesChosen(event: Event) {
  const input = event.target as HTMLInputElement
  const files = Array.from(input.files ?? [])
  input.value = ''
  if (files.length) emit('uploadAttachments', files)
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

function toggleTag(id: string) {
  tagIds.value = tagIds.value.includes(id)
    ? tagIds.value.filter((tagId) => tagId !== id)
    : [...tagIds.value, id]
}
</script>

<template>
  <section :class="cn('min-h-0 flex-col bg-background lg:flex', mobilePane === 'editor' ? 'flex' : 'hidden')">
    <header class="flex h-12 items-center justify-between border-b px-3"><Button class="lg:hidden" variant="ghost" size="sm" @click="emit('back')"><HugeiconsIcon :icon="ArrowLeft01Icon" data-icon="inline-start" />Notes</Button><div class="hidden items-center gap-1 lg:flex"><Button v-for="mode in viewModes" :key="mode.value" :variant="viewMode === mode.value ? 'secondary' : 'ghost'" size="icon-sm" :aria-label="mode.label" :title="mode.label" :aria-pressed="viewMode === mode.value" @click="viewMode = mode.value"><HugeiconsIcon :icon="mode.icon" /></Button></div><div class="flex items-center gap-2"><span v-if="busy" class="text-[10px] text-muted-foreground">Saving…</span><Button v-if="note || fresh" variant="ghost" size="icon-sm" :aria-label="pinned ? 'Unpin note' : 'Pin note'" :title="pinned ? 'Unpin note' : 'Pin note'" :aria-pressed="pinned" @click="pinned = !pinned"><HugeiconsIcon :icon="pinned ? PinOffIcon : PinIcon" /></Button><Button v-if="note && !fresh" variant="ghost" size="icon-sm" aria-label="New note from this template" title="New note from this template" :disabled="busy" @click="emit('useAsTemplate', note)"><HugeiconsIcon :icon="Copy01Icon" /></Button><Button v-if="note" variant="ghost" size="icon-sm" aria-label="Delete note" @click="emit('delete', note)"><HugeiconsIcon :icon="Delete02Icon" /></Button><Button v-if="note || fresh" size="sm" :disabled="busy" @click="emit('save')">{{ fresh ? 'Create' : 'Save' }}</Button></div></header>

    <template v-if="note || fresh">
      <ScrollArea class="flex-1">
        <div class="mx-auto flex w-full max-w-4xl flex-col gap-5 p-5 sm:p-8">
          <FieldGroup>
            <div v-if="!fresh" class="grid gap-3 sm:grid-cols-2">
              <Field><FieldLabel>Notebook</FieldLabel><Select v-model="notebookModel"><SelectTrigger class="w-full"><SelectValue placeholder="Select a notebook" /></SelectTrigger><SelectContent><SelectGroup><SelectItem v-for="book in notebooks" :key="book.id" :value="book.id">{{ notebookPath(notebooks, book.id) }}</SelectItem></SelectGroup></SelectContent></Select></Field>
              <Field><FieldLabel>Status</FieldLabel><Select v-model="statusModel"><SelectTrigger class="w-full"><SelectValue /></SelectTrigger><SelectContent><SelectGroup><SelectItem v-for="item in noteStatuses" :key="item.value" :value="item.value">{{ item.label }}</SelectItem></SelectGroup></SelectContent></Select></Field>
            </div>
          </FieldGroup>

          <FieldSet v-if="!fresh && tags.length"><FieldLegend variant="label">Tags</FieldLegend><FieldGroup class="grid grid-cols-2 gap-2 sm:grid-cols-3"><Field v-for="tag in tags" :key="tag.id" orientation="horizontal"><Checkbox :id="`tag-${tag.id}`" :model-value="tagIds.includes(tag.id)" @update:model-value="toggleTag(tag.id)" /><FieldLabel :for="`tag-${tag.id}`" class="font-normal">{{ tag.name }}</FieldLabel></Field></FieldGroup></FieldSet>

          <div v-if="!fresh && selectedTags.length" class="flex flex-wrap gap-1.5"><Badge v-for="tag in selectedTags" :key="tag.id" variant="secondary">{{ tag.name }}</Badge></div>
          <FieldSet v-if="!fresh && note">
            <FieldLegend variant="label">Attachments</FieldLegend>
            <ul v-if="attachments.length" class="flex flex-col gap-1.5">
              <li v-for="file in attachments" :key="file.id" class="flex items-center gap-2 rounded-lg border px-3 py-2"><HugeiconsIcon :icon="Attachment01Icon" :size="14" class="shrink-0 text-muted-foreground" /><span class="min-w-0 flex-1 truncate text-xs">{{ file.name }}</span><span class="text-[10px] text-muted-foreground">{{ formatSize(file.contentLength) }}</span><Button variant="ghost" size="icon-xs" :aria-label="`Download ${file.name}`" @click="emit('downloadAttachment', file)"><HugeiconsIcon :icon="Download01Icon" /></Button><Button variant="ghost" size="icon-xs" :aria-label="`Delete ${file.name}`" @click="emit('deleteAttachment', file)"><HugeiconsIcon :icon="Delete02Icon" /></Button></li>
            </ul>
            <div><input ref="fileInput" type="file" multiple class="sr-only" tabindex="-1" @change="onFilesChosen" /><Button variant="outline" size="sm" :disabled="busy" @click="fileInput?.click()"><HugeiconsIcon :icon="Attachment01Icon" data-icon="inline-start" />Attach file</Button></div>
          </FieldSet>
          <div :class="cn('grid gap-4', viewMode === 'split' && 'lg:grid-cols-2')">
            <Field v-show="viewMode !== 'preview'"><FieldLabel for="note-content" class="sr-only">Note content</FieldLabel><Textarea id="note-content" v-model="content" class="min-h-[45vh]" spellcheck="true" placeholder="Start writing…" /></Field>
            <div v-if="viewMode !== 'edit'" class="markdown-preview min-h-[45vh] rounded-lg border p-4" aria-label="Preview" v-html="previewHtml" />
          </div>
          <div v-if="note && !fresh" class="flex flex-wrap gap-x-3 text-[10px] text-muted-foreground"><span>Created {{ sourceLabel(note.createdSource) ? `by ${sourceLabel(note.createdSource)}` : 'in the app' }}</span><span>Last edited {{ sourceLabel(note.updatedSource) ? `by ${sourceLabel(note.updatedSource)}` : 'in the app' }}</span><span v-if="note.sourceTemplateId">From template: {{ templateName ?? 'deleted note' }}</span></div>
          <div class="flex items-center justify-between text-[10px] text-muted-foreground"><span>{{ content.length }} characters</span><span><template v-if="!fresh">{{ selectedNotebookName }} · </template>{{ wordCount }} words</span></div>
        </div>
      </ScrollArea>
    </template>
    <Empty v-else class="flex-1"><EmptyHeader><EmptyMedia variant="icon"><HugeiconsIcon :icon="Note01Icon" :size="18" /></EmptyMedia><EmptyTitle>Select a note to begin</EmptyTitle><EmptyDescription>Your ideas are ready when you are.</EmptyDescription></EmptyHeader><EmptyContent><Button :disabled="busy" @click="emit('create')"><HugeiconsIcon :icon="NoteAddIcon" data-icon="inline-start" />Create a new note</Button></EmptyContent></Empty>
  </section>
</template>
