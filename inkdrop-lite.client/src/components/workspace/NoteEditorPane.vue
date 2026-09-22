<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { HugeiconsIcon } from '@hugeicons/vue'
import {
  Add01Icon,
  ArrowLeft01Icon,
  Attachment01Icon,
  CheckListIcon,
  Copy01Icon,
  Delete02Icon,
  Download01Icon,
  Edit02Icon,
  Heading01Icon,
  Layout2ColumnIcon,
  LeftToRightListBulletIcon,
  LeftToRightListNumberIcon,
  Note01Icon,
  NoteAddIcon,
  PinIcon,
  PinOffIcon,
  QuoteUpIcon,
  SeparatorHorizontalIcon,
  SourceCodeIcon,
  Table01Icon,
  ViewIcon,
} from '@hugeicons/core-free-icons'

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

const title = defineModel<string>('title', { required: true })
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
const selectedTags = computed(() => props.tags.filter((tag) => tagIds.value.includes(tag.id)))

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
const contentInput = ref<InstanceType<typeof Textarea> | null>(null)

function contentEl() {
  return contentInput.value?.$el as HTMLTextAreaElement | undefined
}

function focusContent() {
  contentEl()?.focus()
}

// Inserts markdown at the start of the current line (heading/list/quote-style prefixes).
function applyLinePrefix(prefix: string) {
  const el = contentEl()
  if (!el) return
  const start = el.selectionStart
  const value = content.value
  const lineStart = value.lastIndexOf('\n', start - 1) + 1
  content.value = value.slice(0, lineStart) + prefix + value.slice(lineStart)
  const pos = start + prefix.length
  nextTick(() => { el.focus(); el.setSelectionRange(pos, pos) })
}

// Inserts a standalone block (code fence, divider, table) at the cursor, on its own line.
function insertBlock(template: string, cursorOffsetFromEnd = 0) {
  const el = contentEl()
  if (!el) return
  const start = el.selectionStart
  const end = el.selectionEnd
  const value = content.value
  const needsLeadingNewline = start > 0 && value[start - 1] !== '\n'
  const insertion = (needsLeadingNewline ? '\n' : '') + template
  content.value = value.slice(0, start) + insertion + value.slice(end)
  const pos = start + insertion.length - cursorOffsetFromEnd
  nextTick(() => { el.focus(); el.setSelectionRange(pos, pos) })
}

const showInsertMenu = ref(false)
const insertOptions = [
  { label: 'Heading', icon: Heading01Icon, action: () => applyLinePrefix('## ') },
  { label: 'Bulleted list', icon: LeftToRightListBulletIcon, action: () => applyLinePrefix('- ') },
  { label: 'Numbered list', icon: LeftToRightListNumberIcon, action: () => applyLinePrefix('1. ') },
  { label: 'To-do', icon: CheckListIcon, action: () => applyLinePrefix('- [ ] ') },
  { label: 'Quote', icon: QuoteUpIcon, action: () => applyLinePrefix('> ') },
  { label: 'Code block', icon: SourceCodeIcon, action: () => insertBlock('```\n\n```', 4) },
  { label: 'Table', icon: Table01Icon, action: () => insertBlock('| Column 1 | Column 2 |\n| --- | --- |\n|  |  |\n') },
  { label: 'Divider', icon: SeparatorHorizontalIcon, action: () => insertBlock('---\n') },
]

function runInsert(action: () => void) {
  showInsertMenu.value = false
  action()
}

// Tracks which text line the mouse is over so a Notion-style "+" can sit in the
// left gutter next to that row. Approximate: it maps pixel position to a raw
// Markdown line via line-height, so it is exact for single-line rows (headings,
// list items) and lands on the paragraph's first line for long, wrapped ones.
const hoverLine = ref<number | null>(null)
const hoverButtonTop = ref(0)
const hoverLineHeight = ref(24)

function onContentMouseMove(event: MouseEvent) {
  const el = contentEl()
  if (!el || showInsertMenu.value) return
  const cs = getComputedStyle(el)
  const lineHeight = parseFloat(cs.lineHeight) || 24
  const paddingTop = parseFloat(cs.paddingTop) || 0
  const rect = el.getBoundingClientRect()
  const y = event.clientY - rect.top - paddingTop + el.scrollTop
  const totalLines = content.value.split('\n').length
  const line = Math.min(totalLines - 1, Math.max(0, Math.floor(y / lineHeight)))
  hoverLine.value = line
  hoverButtonTop.value = paddingTop + line * lineHeight - el.scrollTop
  hoverLineHeight.value = lineHeight
}

function onContentMouseLeave() {
  if (!showInsertMenu.value) hoverLine.value = null
}

function lineStartOffset(line: number) {
  const lines = content.value.split('\n')
  let offset = 0
  for (let i = 0; i < line; i++) offset += lines[i]!.length + 1
  return offset
}

function openInsertMenuAtHover() {
  const el = contentEl()
  if (!el || hoverLine.value === null) return
  const pos = lineStartOffset(hoverLine.value)
  el.focus()
  el.setSelectionRange(pos, pos)
  showInsertMenu.value = true
}

// A lightweight outline of the note, built from Markdown headings (# / ## / ###).
type Heading = { level: number; text: string; line: number }
const headings = computed<Heading[]>(() => {
  const result: Heading[] = []
  content.value.split('\n').forEach((line, index) => {
    const match = /^(#{1,3})\s+(.+)/.exec(line)
    if (match) result.push({ level: match[1]!.length, text: match[2]!.trim(), line: index })
  })
  return result
})
const activeHeadingLine = ref<number | null>(null)

function onContentScroll() {
  const el = contentEl()
  if (!el || !headings.value.length) return
  const totalLines = content.value.split('\n').length
  const scrollable = el.scrollHeight - el.clientHeight
  const approxLine = scrollable > 0 ? Math.round((el.scrollTop / scrollable) * totalLines) : 0
  let current = headings.value[0]!.line
  for (const heading of headings.value) {
    if (heading.line <= approxLine) current = heading.line
    else break
  }
  activeHeadingLine.value = current
}

function jumpToHeading(line: number) {
  const el = contentEl()
  if (!el) return
  const totalLines = content.value.split('\n').length
  const scrollable = el.scrollHeight - el.clientHeight
  el.scrollTop = totalLines > 1 ? (line / (totalLines - 1)) * scrollable : 0
  activeHeadingLine.value = line
  el.focus()
}

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

    <nav v-if="(note || fresh) && headings.length && viewMode !== 'preview'" class="fixed top-16 right-6 z-10 hidden w-52 flex-col gap-0.5 xl:flex" aria-label="Note outline">
      <button v-for="heading in headings" :key="heading.line" type="button" :class="cn('truncate rounded px-2 py-1 text-left text-xs text-muted-foreground hover:text-foreground', heading.level > 1 && 'pl-4', heading.level > 2 && 'pl-6', activeHeadingLine === heading.line && 'font-medium text-foreground')" @click="jumpToHeading(heading.line)">{{ heading.text }}</button>
    </nav>

    <template v-if="note || fresh">
      <ScrollArea class="flex-1">
        <div class="mx-auto flex w-full max-w-6xl flex-col gap-4 px-6 py-10 sm:px-14">
          <label for="note-title" class="sr-only">Title</label>
          <input id="note-title" v-model="title" type="text" maxlength="256" placeholder="Untitled" class="w-full border-none bg-transparent text-3xl font-bold tracking-tight text-foreground outline-none placeholder:text-muted-foreground/40 sm:text-4xl" @keydown.enter.prevent="focusContent" />

          <div v-if="!fresh" class="flex flex-wrap items-center gap-x-6 gap-y-3 text-sm text-muted-foreground">
            <div class="flex items-center gap-2"><span class="shrink-0">Notebook</span><Select v-model="notebookModel"><SelectTrigger class="h-8 w-56 border-none bg-transparent shadow-none hover:bg-accent dark:bg-transparent dark:hover:bg-accent"><SelectValue placeholder="Select a notebook" /></SelectTrigger><SelectContent><SelectGroup><SelectItem v-for="book in notebooks" :key="book.id" :value="book.id">{{ notebookPath(notebooks, book.id) }}</SelectItem></SelectGroup></SelectContent></Select></div>
            <div class="flex items-center gap-2"><span class="shrink-0">Status</span><Select v-model="statusModel"><SelectTrigger class="h-8 w-40 border-none bg-transparent shadow-none hover:bg-accent dark:bg-transparent dark:hover:bg-accent"><SelectValue /></SelectTrigger><SelectContent><SelectGroup><SelectItem v-for="item in noteStatuses" :key="item.value" :value="item.value">{{ item.label }}</SelectItem></SelectGroup></SelectContent></Select></div>
          </div>

          <FieldSet v-if="!fresh && tags.length"><FieldLegend variant="label">Tags</FieldLegend><FieldGroup class="grid grid-cols-2 gap-2 sm:grid-cols-3"><Field v-for="tag in tags" :key="tag.id" orientation="horizontal"><Checkbox :id="`tag-${tag.id}`" :model-value="tagIds.includes(tag.id)" @update:model-value="toggleTag(tag.id)" /><FieldLabel :for="`tag-${tag.id}`" class="font-normal">{{ tag.name }}</FieldLabel></Field></FieldGroup></FieldSet>

          <div v-if="!fresh && selectedTags.length" class="flex flex-wrap gap-1.5"><Badge v-for="tag in selectedTags" :key="tag.id" variant="secondary">{{ tag.name }}</Badge></div>
          <FieldSet v-if="!fresh && note">
            <FieldLegend variant="label">Attachments</FieldLegend>
            <ul v-if="attachments.length" class="flex flex-col gap-1.5">
              <li v-for="file in attachments" :key="file.id" class="flex items-center gap-2 rounded-lg border px-3 py-2"><HugeiconsIcon :icon="Attachment01Icon" :size="14" class="shrink-0 text-muted-foreground" /><span class="min-w-0 flex-1 truncate text-xs">{{ file.name }}</span><span class="text-[10px] text-muted-foreground">{{ formatSize(file.contentLength) }}</span><Button variant="ghost" size="icon-xs" :aria-label="`Download ${file.name}`" @click="emit('downloadAttachment', file)"><HugeiconsIcon :icon="Download01Icon" /></Button><Button variant="ghost" size="icon-xs" :aria-label="`Delete ${file.name}`" @click="emit('deleteAttachment', file)"><HugeiconsIcon :icon="Delete02Icon" /></Button></li>
            </ul>
            <div><input ref="fileInput" type="file" multiple class="sr-only" tabindex="-1" @change="onFilesChosen" /><Button variant="outline" size="sm" :disabled="busy" @click="fileInput?.click()"><HugeiconsIcon :icon="Attachment01Icon" data-icon="inline-start" />Attach file</Button></div>
          </FieldSet>
          <div class="flex items-start pt-2" @mousemove="viewMode !== 'preview' && onContentMouseMove($event)" @mouseleave="onContentMouseLeave">
            <div class="relative hidden w-8 shrink-0 sm:block">
              <div v-if="viewMode !== 'preview' && hoverLine !== null" class="absolute left-0 z-20 flex w-8 items-center justify-center" :style="{ top: `${hoverButtonTop}px`, height: `${hoverLineHeight}px` }">
                <Button variant="ghost" size="icon-sm" aria-label="Insert block here" title="Insert block here" @click="openInsertMenuAtHover"><HugeiconsIcon :icon="Add01Icon" /></Button>
                <template v-if="showInsertMenu">
                  <div class="fixed inset-0 z-40" @click="showInsertMenu = false" />
                  <div class="absolute left-0 top-full z-50 mt-1 flex w-48 flex-col rounded-lg border bg-popover p-1 text-popover-foreground shadow-lg">
                    <button v-for="option in insertOptions" :key="option.label" type="button" class="flex items-center gap-2 rounded-md px-2 py-1.5 text-left text-xs hover:bg-accent" @click="runInsert(option.action)"><HugeiconsIcon :icon="option.icon" :size="14" class="text-muted-foreground" />{{ option.label }}</button>
                  </div>
                </template>
              </div>
            </div>
            <div class="min-w-0 flex-1">
              <div :class="cn('grid gap-4', viewMode === 'split' && 'lg:grid-cols-2')">
                <Field v-show="viewMode !== 'preview'"><FieldLabel for="note-content" class="sr-only">Note content</FieldLabel><Textarea id="note-content" ref="contentInput" v-model="content" class="min-h-[calc(100dvh-14rem)] resize-none border-0 bg-transparent p-0 text-base leading-7 shadow-none focus-visible:ring-0 md:text-base dark:bg-transparent" spellcheck="true" placeholder="Write something…" @scroll="onContentScroll" /></Field>
                <div v-if="viewMode !== 'edit'" :class="cn('markdown-preview min-h-[calc(100dvh-14rem)]', viewMode === 'split' && 'lg:border-l lg:pl-8')" aria-label="Preview" v-html="previewHtml" />
              </div>
            </div>
          </div>
          <div v-if="note && !fresh" class="flex flex-wrap gap-x-3 border-t pt-4 text-[10px] text-muted-foreground"><span>Created {{ sourceLabel(note.createdSource) ? `by ${sourceLabel(note.createdSource)}` : 'in the app' }}</span><span>Last edited {{ sourceLabel(note.updatedSource) ? `by ${sourceLabel(note.updatedSource)}` : 'in the app' }}</span><span v-if="note.sourceTemplateId">From template: {{ templateName ?? 'deleted note' }}</span></div>
        </div>
      </ScrollArea>
    </template>
    <Empty v-else class="flex-1"><EmptyHeader><EmptyMedia variant="icon"><HugeiconsIcon :icon="Note01Icon" :size="18" /></EmptyMedia><EmptyTitle>Select a note to begin</EmptyTitle><EmptyDescription>Your ideas are ready when you are.</EmptyDescription></EmptyHeader><EmptyContent><Button :disabled="busy" @click="emit('create')"><HugeiconsIcon :icon="NoteAddIcon" data-icon="inline-start" />Create a new note</Button></EmptyContent></Empty>
  </section>
</template>
