<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { HugeiconsIcon } from '@hugeicons/vue'
import {
  Add01Icon,
  ArrowLeft01Icon,
  BookOpen01Icon,
  Cancel01Icon,
  CheckmarkCircle02Icon,
  Delete02Icon,
  Edit02Icon,
  File01Icon,
  Folder01Icon,
  Layout2ColumnIcon,
  Logout03Icon,
  Menu01Icon,
  Note01Icon,
  NoteAddIcon,
  PauseCircleIcon,
  PlayCircleIcon,
  Search01Icon,
  Tag01Icon,
  ViewIcon,
} from '@hugeicons/core-free-icons'

import { ApiError, checkApiHealth } from '@/api/client'
import { notebooksApi, type NotebookResponse } from '@/api/notebooks'
import { notesApi, NoteStatus, type NoteResponse } from '@/api/notes'
import { tagsApi, type TagResponse } from '@/api/tags'
import { getCurrentAccount, initializeAuth, signIn, signOut } from '@/auth/entra'
import { cn } from '@/lib/utils'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { Field, FieldGroup, FieldLabel, FieldSet, FieldLegend } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/components/ui/input-group'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Separator } from '@/components/ui/separator'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Textarea } from '@/components/ui/textarea'

type NavFilter =
  | { kind: 'all'; label: string }
  | { kind: 'notebook'; id: string; label: string }
  | { kind: 'status'; id: NoteStatus; label: string }
  | { kind: 'tag'; id: string; label: string }

type DeleteTarget =
  | { kind: 'note'; item: NoteResponse }
  | { kind: 'notebook'; item: NotebookResponse }
  | { kind: 'tag'; item: TagResponse }

const apiOnline = ref<boolean | null>(null)
const accountName = ref<string | null>(null)
const notes = ref<NoteResponse[]>([])
const notebooks = ref<NotebookResponse[]>([])
const tags = ref<TagResponse[]>([])
const busyCount = ref(0)
const error = ref<string | null>(null)
const query = ref('')
const selectedNoteId = ref<string | null>(null)
const activeFilter = ref<NavFilter>({ kind: 'all', label: 'All Notes' })
const mobilePane = ref<'library' | 'notes' | 'editor'>('notes')
const showManager = ref(false)
const managerTab = ref<'notebooks' | 'tags'>('notebooks')
const editingNotebookId = ref<string | null>(null)
const editingTagId = ref<string | null>(null)
const isNewNote = ref(false)
const deleteTarget = ref<DeleteTarget | null>(null)

const busy = computed(() => busyCount.value > 0)
const deleteDialogOpen = computed({
  get: () => deleteTarget.value !== null,
  set: (value) => { if (!value) deleteTarget.value = null },
})
const noteForm = reactive({ title: '', content: '', status: NoteStatus.Active, notebookId: '', tagIds: [] as string[] })
const notebookForm = reactive({ name: '', description: '' })
const tagForm = reactive({ name: '' })
const noteStatuses = [
  { value: NoteStatus.Active, label: 'Active', icon: PlayCircleIcon },
  { value: NoteStatus.OnHold, label: 'On Hold', icon: PauseCircleIcon },
  { value: NoteStatus.Completed, label: 'Completed', icon: CheckmarkCircle02Icon },
  { value: NoteStatus.Dropped, label: 'Dropped', icon: Cancel01Icon },
]
const statusModel = computed({
  get: () => String(noteForm.status),
  set: (value: string) => { noteForm.status = Number(value) as NoteStatus },
})
const notebookModel = computed({
  get: () => noteForm.notebookId || 'none',
  set: (value: string) => { noteForm.notebookId = value === 'none' ? '' : value },
})

const filteredNotes = computed(() => {
  let result = notes.value
  const filter = activeFilter.value
  if (filter.kind === 'notebook') result = result.filter((note) => note.notebookId === filter.id)
  if (filter.kind === 'status') result = result.filter((note) => note.status === filter.id)
  if (filter.kind === 'tag') result = result.filter((note) => note.tagIds?.includes(filter.id))
  const term = query.value.trim().toLocaleLowerCase()
  if (term) result = result.filter((note) => `${note.title} ${note.content}`.toLocaleLowerCase().includes(term))
  return [...result].sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime())
})
const selectedNote = computed(() => notes.value.find((note) => note.id === selectedNoteId.value) ?? null)
const selectedNotebookName = computed(() => notebooks.value.find((book) => book.id === noteForm.notebookId)?.name ?? 'No notebook')
const selectedTags = computed(() => tags.value.filter((tag) => noteForm.tagIds.includes(tag.id)))

onMounted(async () => execute(async () => {
  await initializeAuth()
  accountName.value = getCurrentAccount()?.username ?? null
  apiOnline.value = await checkApiHealth()
  if (accountName.value) await loadAll()
}, false))

watch(filteredNotes, (items) => {
  if (!isNewNote.value && items.length && !items.some((note) => note.id === selectedNoteId.value)) selectNote(items[0]!)
}, { flush: 'post' })

async function connect() { await execute(async () => { accountName.value = (await signIn()).username; await loadAll() }, false) }
async function disconnect() { await execute(async () => { await signOut(); accountName.value = null; notes.value = []; notebooks.value = []; tags.value = [] }, false) }
async function loadAll() {
  const [noteData, notebookData, tagData] = await Promise.all([notesApi.getAll(), notebooksApi.getAll(), tagsApi.getAll()])
  notes.value = noteData; notebooks.value = notebookData; tags.value = tagData
  if (noteData.length) selectNote(noteData[0]!)
}
function setFilter(filter: NavFilter) { activeFilter.value = filter; isNewNote.value = false; mobilePane.value = 'notes' }
function selectNote(note: NoteResponse) {
  selectedNoteId.value = note.id; isNewNote.value = false
  Object.assign(noteForm, { title: note.title, content: note.content, status: note.status, notebookId: note.notebookId ?? '', tagIds: [...(note.tagIds ?? [])] })
  mobilePane.value = 'editor'
}
function newNote() {
  selectedNoteId.value = null; isNewNote.value = true
  Object.assign(noteForm, { title: '', content: '', status: NoteStatus.Active, notebookId: activeFilter.value.kind === 'notebook' ? activeFilter.value.id : '', tagIds: activeFilter.value.kind === 'tag' ? [activeFilter.value.id] : [] })
  mobilePane.value = 'editor'
}
async function saveNote() { await execute(async () => {
  const payload = { ...noteForm, title: noteForm.title.trim() || 'Untitled', notebookId: noteForm.notebookId || null }
  if (selectedNoteId.value) await notesApi.update(selectedNoteId.value, payload)
  else selectedNoteId.value = (await notesApi.create(payload)).id
  notes.value = await notesApi.getAll()
  const saved = notes.value.find((note) => note.id === selectedNoteId.value)
  if (saved) selectNote(saved)
  isNewNote.value = false
}) }
function toggleTag(id: string) { const index = noteForm.tagIds.indexOf(id); if (index >= 0) noteForm.tagIds.splice(index, 1); else noteForm.tagIds.push(id) }
async function saveNotebook() { await execute(async () => { const payload = { name: notebookForm.name.trim(), description: notebookForm.description.trim() || null }; if (editingNotebookId.value) await notebooksApi.update(editingNotebookId.value, payload); else await notebooksApi.create(payload); notebooks.value = await notebooksApi.getAll(); resetNotebook() }) }
function editNotebook(book: NotebookResponse) { editingNotebookId.value = book.id; Object.assign(notebookForm, { name: book.name, description: book.description ?? '' }) }
function resetNotebook() { editingNotebookId.value = null; Object.assign(notebookForm, { name: '', description: '' }) }
async function saveTag() { await execute(async () => { const payload = { name: tagForm.name.trim() }; if (editingTagId.value) await tagsApi.update(editingTagId.value, payload); else await tagsApi.create(payload); tags.value = await tagsApi.getAll(); resetTag() }) }
function editTag(tag: TagResponse) { editingTagId.value = tag.id; tagForm.name = tag.name }
function resetTag() { editingTagId.value = null; tagForm.name = '' }
function requestDelete(target: DeleteTarget) { deleteTarget.value = target }
async function confirmDelete() {
  const target = deleteTarget.value
  if (!target) return
  await execute(async () => {
    if (target.kind === 'note') {
      await notesApi.delete(target.item.id)
      notes.value = notes.value.filter((item) => item.id !== target.item.id)
      selectedNoteId.value = null
      if (filteredNotes.value.length) selectNote(filteredNotes.value[0]!)
      else newNote()
    } else if (target.kind === 'notebook') {
      await notebooksApi.delete(target.item.id)
      await loadAll()
    } else {
      await tagsApi.delete(target.item.id)
      tags.value = await tagsApi.getAll()
    }
    deleteTarget.value = null
  })
}
function noteCount(kind: 'notebook' | 'tag' | 'status', id: string | NoteStatus) { if (kind === 'notebook') return notes.value.filter((note) => note.notebookId === id).length; if (kind === 'tag') return notes.value.filter((note) => note.tagIds?.includes(id as string)).length; return notes.value.filter((note) => note.status === id).length }
function excerpt(content: string) { return content.replace(/[#>*_`\-[\]()]/g, ' ').replace(/\s+/g, ' ').trim() || 'Empty note' }
function relativeDate(value: string) { const minutes = Math.max(1, Math.floor((Date.now() - new Date(value).getTime()) / 60000)); if (minutes < 60) return `${minutes}m ago`; const hours = Math.floor(minutes / 60); if (hours < 24) return `${hours}h ago`; const days = Math.floor(hours / 24); if (days < 7) return `${days}d ago`; return new Intl.DateTimeFormat('en', { month: 'short', day: 'numeric' }).format(new Date(value)) }
function initials(value: string) { const name = value.split('@')[0] ?? value; return name.split(/[._-]/).map((part) => part[0] ?? '').join('').slice(0, 2).toUpperCase() }
function deleteTargetName() { const target = deleteTarget.value; if (!target) return ''; return target.kind === 'note' ? target.item.title : target.item.name }
async function execute(action: () => Promise<void>, clearError = true) { busyCount.value++; if (clearError) error.value = null; try { await action() } catch (cause) { error.value = cause instanceof ApiError || cause instanceof Error ? cause.message : 'Something went wrong.' } finally { busyCount.value-- } }
</script>

<template>
  <main v-if="!accountName" class="min-h-dvh overflow-auto bg-background text-foreground">
    <header class="mx-auto flex h-20 max-w-7xl items-center justify-between px-6">
      <a class="flex items-center gap-2 text-sm font-semibold" href="#">
        <span class="grid size-8 place-items-center rounded-xl bg-primary text-primary-foreground"><HugeiconsIcon :icon="Note01Icon" :size="16" /></span>
        <span>Inkdrop Lite</span>
      </a>
      <Badge variant="outline"><span :class="cn('size-1.5 rounded-full', apiOnline ? 'bg-primary' : 'bg-muted-foreground')"></span>{{ apiOnline ? 'API online' : 'Checking API' }}</Badge>
    </header>

    <section class="mx-auto grid min-h-[calc(100dvh-5rem)] max-w-7xl items-center gap-14 px-6 py-12 lg:grid-cols-[0.9fr_1.1fr]">
      <div class="mx-auto flex max-w-xl flex-col items-start gap-6 lg:mx-0">
        <Badge variant="secondary">Your private writing space</Badge>
        <div class="flex flex-col gap-4">
          <h1 class="text-5xl font-semibold tracking-[-0.05em] text-balance sm:text-6xl">Your context, beautifully organized.</h1>
          <p class="max-w-lg text-base leading-7 text-muted-foreground">Keep notes, projects, and the small decisions that move your work forward in one calm Markdown workspace.</p>
        </div>
        <Button size="lg" :disabled="busy" @click="connect"><HugeiconsIcon :icon="BookOpen01Icon" data-icon="inline-start" />{{ busy ? 'Connecting…' : 'Open your workspace' }}</Button>
        <p class="text-xs text-muted-foreground">Secure sign in with your Microsoft account</p>
      </div>

      <Card class="hidden overflow-hidden shadow-2xl md:block">
        <CardHeader class="border-b">
          <CardTitle>Inkdrop Lite</CardTitle>
          <CardDescription>A focused space for everything worth remembering.</CardDescription>
        </CardHeader>
        <CardContent class="grid min-h-[26rem] grid-cols-[9rem_13rem_1fr] p-0">
          <div class="flex flex-col gap-2 border-r bg-muted/40 p-4 text-xs"><b class="mb-3">All Notes <span class="float-right text-muted-foreground">24</span></b><span class="text-muted-foreground">NOTEBOOKS</span><span>Inbox</span><span>Work</span><span>Ideas</span><Separator class="my-3" /><span class="text-muted-foreground">STATUS</span><span>Active</span><span>Completed</span></div>
          <div class="flex flex-col gap-3 border-r p-4"><b class="text-xs">All Notes</b><div class="h-7 rounded-md bg-muted"></div><div class="flex flex-col gap-1 rounded-md bg-accent p-3"><b class="text-xs">Building a calmer workflow</b><span class="text-[10px] text-muted-foreground">Today · work</span><p class="line-clamp-2 text-[10px] text-muted-foreground">Small systems compound over time...</p></div><div class="flex flex-col gap-1 p-3"><b class="text-xs">Project notes</b><span class="text-[10px] text-muted-foreground">Yesterday · ideas</span></div></div>
          <div class="flex flex-col gap-6 p-8"><Badge variant="outline">WORK · ACTIVE</Badge><h2 class="text-2xl font-semibold">Building a calmer workflow</h2><div class="flex flex-col gap-3 text-sm"><b>## The idea</b><p class="leading-6 text-muted-foreground">Capture context while it is fresh, keep it easy to retrieve, and let the system stay out of the way.</p></div></div>
        </CardContent>
        <CardFooter class="border-t text-xs text-muted-foreground">Markdown notes, notebooks, statuses and tags in one place.</CardFooter>
      </Card>
    </section>
  </main>

  <main v-else class="dark h-dvh overflow-hidden bg-background text-foreground">
    <header class="flex h-12 items-center justify-between border-b px-3 lg:hidden">
      <Button variant="ghost" size="icon" aria-label="Open library" @click="mobilePane = 'library'"><HugeiconsIcon :icon="Menu01Icon" /></Button>
      <span class="text-xs font-medium">{{ activeFilter.label }}</span>
      <Button variant="ghost" size="icon" aria-label="New note" @click="newNote"><HugeiconsIcon :icon="Add01Icon" /></Button>
    </header>

    <div class="grid h-[calc(100dvh-3rem)] lg:h-dvh lg:grid-cols-[16rem_22rem_minmax(0,1fr)]">
      <aside :class="cn('min-h-0 flex-col border-r bg-sidebar text-sidebar-foreground lg:flex', mobilePane === 'library' ? 'flex' : 'hidden')">
        <div class="hidden h-12 items-center gap-2 border-b px-4 lg:flex"><span class="grid size-7 place-items-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground"><HugeiconsIcon :icon="Note01Icon" :size="14" /></span><b class="text-xs">Inkdrop Lite</b></div>
        <ScrollArea class="flex-1">
          <nav class="flex flex-col gap-1 p-2">
            <Button :variant="activeFilter.kind === 'all' ? 'secondary' : 'ghost'" class="w-full justify-start" @click="setFilter({ kind: 'all', label: 'All Notes' })"><HugeiconsIcon :icon="File01Icon" data-icon="inline-start" /><span class="flex-1 text-left">All Notes</span><Badge variant="outline">{{ notes.length }}</Badge></Button>
            <Button variant="ghost" class="w-full justify-start" @click="newNote"><HugeiconsIcon :icon="NoteAddIcon" data-icon="inline-start" />New note</Button>

            <div class="mt-4 flex items-center justify-between px-2"><span class="text-[10px] font-medium uppercase tracking-widest text-muted-foreground">Notebooks</span><Button variant="ghost" size="icon-xs" aria-label="Manage notebooks" @click="showManager = true; managerTab = 'notebooks'"><HugeiconsIcon :icon="Add01Icon" /></Button></div>
            <Button v-for="book in notebooks" :key="book.id" :variant="activeFilter.kind === 'notebook' && activeFilter.id === book.id ? 'secondary' : 'ghost'" class="w-full justify-start" @click="setFilter({ kind: 'notebook', id: book.id, label: book.name })"><HugeiconsIcon :icon="Folder01Icon" data-icon="inline-start" /><span class="flex-1 truncate text-left">{{ book.name }}</span><span class="text-[10px] text-muted-foreground">{{ noteCount('notebook', book.id) }}</span></Button>
            <p v-if="!notebooks.length" class="px-2 py-2 text-xs text-muted-foreground">No notebooks yet</p>

            <div class="mt-4 px-2"><span class="text-[10px] font-medium uppercase tracking-widest text-muted-foreground">Status</span></div>
            <Button v-for="status in noteStatuses" :key="status.value" :variant="activeFilter.kind === 'status' && activeFilter.id === status.value ? 'secondary' : 'ghost'" class="w-full justify-start" @click="setFilter({ kind: 'status', id: status.value, label: status.label })"><HugeiconsIcon :icon="status.icon" data-icon="inline-start" /><span class="flex-1 text-left">{{ status.label }}</span><span class="text-[10px] text-muted-foreground">{{ noteCount('status', status.value) }}</span></Button>

            <div class="mt-4 flex items-center justify-between px-2"><span class="text-[10px] font-medium uppercase tracking-widest text-muted-foreground">Tags</span><Button variant="ghost" size="icon-xs" aria-label="Manage tags" @click="showManager = true; managerTab = 'tags'"><HugeiconsIcon :icon="Add01Icon" /></Button></div>
            <Button v-for="tag in tags" :key="tag.id" :variant="activeFilter.kind === 'tag' && activeFilter.id === tag.id ? 'secondary' : 'ghost'" class="w-full justify-start" @click="setFilter({ kind: 'tag', id: tag.id, label: tag.name })"><HugeiconsIcon :icon="Tag01Icon" data-icon="inline-start" /><span class="flex-1 truncate text-left">{{ tag.name }}</span><span class="text-[10px] text-muted-foreground">{{ noteCount('tag', tag.id) }}</span></Button>
          </nav>
        </ScrollArea>
        <Separator />
        <div class="flex items-center gap-2 p-3"><Avatar size="sm"><AvatarFallback>{{ initials(accountName) }}</AvatarFallback></Avatar><div class="min-w-0 flex-1"><b class="block truncate text-xs">{{ accountName.split('@')[0] }}</b><span class="block truncate text-[10px] text-muted-foreground">{{ accountName }}</span></div><Button variant="ghost" size="icon-sm" title="Sign out" :disabled="busy" @click="disconnect"><HugeiconsIcon :icon="Logout03Icon" /></Button></div>
      </aside>

      <section :class="cn('min-h-0 flex-col border-r bg-card lg:flex', mobilePane === 'notes' ? 'flex' : 'hidden')">
        <header class="flex h-16 items-center justify-between border-b px-4"><Button class="lg:hidden" variant="ghost" size="icon-sm" @click="mobilePane = 'library'"><HugeiconsIcon :icon="ArrowLeft01Icon" /></Button><div class="min-w-0 flex-1"><h2 class="truncate text-sm font-medium">{{ activeFilter.label }}</h2><p class="text-[10px] text-muted-foreground">{{ filteredNotes.length }} {{ filteredNotes.length === 1 ? 'note' : 'notes' }}</p></div><Button size="icon-sm" aria-label="Create note" @click="newNote"><HugeiconsIcon :icon="Add01Icon" /></Button></header>
        <div class="p-3"><InputGroup><InputGroupAddon><HugeiconsIcon :icon="Search01Icon" :size="14" /></InputGroupAddon><InputGroupInput v-model="query" type="search" placeholder="Filter notes…" /></InputGroup></div>
        <ScrollArea class="flex-1">
          <div v-if="filteredNotes.length" class="flex flex-col">
            <button v-for="note in filteredNotes" :key="note.id" :class="cn('flex flex-col gap-2 border-b p-4 text-left transition-colors hover:bg-muted/50', selectedNoteId === note.id && 'bg-accent')" @click="selectNote(note)">
              <div class="flex items-start gap-2"><strong class="flex-1 text-xs font-medium leading-5">{{ note.title || 'Untitled' }}</strong><Badge v-if="note.status === NoteStatus.Active" variant="secondary">Active</Badge></div>
              <div class="flex flex-wrap items-center gap-1.5"><time class="text-[10px] text-muted-foreground">{{ relativeDate(note.updatedAt) }}</time><Badge v-for="tag in tags.filter((item) => note.tagIds?.includes(item.id)).slice(0, 2)" :key="tag.id" variant="outline">{{ tag.name }}</Badge></div>
              <p class="truncate text-[11px] text-muted-foreground">{{ excerpt(note.content) }}</p>
            </button>
          </div>
          <Empty v-else class="min-h-80"><EmptyHeader><EmptyMedia variant="icon"><HugeiconsIcon :icon="Note01Icon" :size="18" /></EmptyMedia><EmptyTitle>No notes here</EmptyTitle><EmptyDescription>Create a note and start writing.</EmptyDescription></EmptyHeader><EmptyContent><Button size="sm" @click="newNote"><HugeiconsIcon :icon="NoteAddIcon" data-icon="inline-start" />Create note</Button></EmptyContent></Empty>
        </ScrollArea>
      </section>

      <section :class="cn('min-h-0 flex-col bg-background lg:flex', mobilePane === 'editor' ? 'flex' : 'hidden')">
        <header class="flex h-12 items-center justify-between border-b px-3"><Button class="lg:hidden" variant="ghost" size="sm" @click="mobilePane = 'notes'"><HugeiconsIcon :icon="ArrowLeft01Icon" data-icon="inline-start" />Notes</Button><div class="hidden items-center gap-1 lg:flex"><Button variant="secondary" size="icon-sm"><HugeiconsIcon :icon="Edit02Icon" /></Button><Button variant="ghost" size="icon-sm"><HugeiconsIcon :icon="Layout2ColumnIcon" /></Button><Button variant="ghost" size="icon-sm"><HugeiconsIcon :icon="ViewIcon" /></Button></div><div class="flex items-center gap-2"><span v-if="busy" class="text-[10px] text-muted-foreground">Saving…</span><Button v-if="selectedNote" variant="ghost" size="icon-sm" aria-label="Delete note" @click="requestDelete({ kind: 'note', item: selectedNote })"><HugeiconsIcon :icon="Delete02Icon" /></Button><Button size="sm" :disabled="busy" @click="saveNote">{{ isNewNote ? 'Create' : 'Save' }}</Button></div></header>

        <template v-if="selectedNote || isNewNote">
          <ScrollArea class="flex-1">
            <div class="mx-auto flex w-full max-w-4xl flex-col gap-5 p-5 sm:p-8">
              <FieldGroup>
                <Field><FieldLabel for="note-title" class="sr-only">Note title</FieldLabel><Input id="note-title" v-model="noteForm.title" maxlength="200" placeholder="Untitled" /></Field>
                <div class="grid gap-3 sm:grid-cols-2">
                  <Field><FieldLabel>Notebook</FieldLabel><Select v-model="notebookModel"><SelectTrigger class="w-full"><SelectValue placeholder="No notebook" /></SelectTrigger><SelectContent><SelectGroup><SelectItem value="none">No notebook</SelectItem><SelectItem v-for="book in notebooks" :key="book.id" :value="book.id">{{ book.name }}</SelectItem></SelectGroup></SelectContent></Select></Field>
                  <Field><FieldLabel>Status</FieldLabel><Select v-model="statusModel"><SelectTrigger class="w-full"><SelectValue /></SelectTrigger><SelectContent><SelectGroup><SelectItem v-for="status in noteStatuses" :key="status.value" :value="String(status.value)">{{ status.label }}</SelectItem></SelectGroup></SelectContent></Select></Field>
                </div>
              </FieldGroup>

              <FieldSet v-if="tags.length"><FieldLegend variant="label">Tags</FieldLegend><FieldGroup class="grid grid-cols-2 gap-2 sm:grid-cols-3"><Field v-for="tag in tags" :key="tag.id" orientation="horizontal"><Checkbox :id="`tag-${tag.id}`" :model-value="noteForm.tagIds.includes(tag.id)" @update:model-value="toggleTag(tag.id)" /><FieldLabel :for="`tag-${tag.id}`" class="font-normal">{{ tag.name }}</FieldLabel></Field></FieldGroup></FieldSet>

              <div v-if="selectedTags.length" class="flex flex-wrap gap-1.5"><Badge v-for="tag in selectedTags" :key="tag.id" variant="secondary">{{ tag.name }}</Badge></div>
              <Field><FieldLabel for="note-content">Markdown</FieldLabel><Textarea id="note-content" v-model="noteForm.content" class="min-h-[45vh] font-mono" spellcheck="true" placeholder="Start writing in Markdown…" /></Field>
              <div class="flex items-center justify-between text-[10px] text-muted-foreground"><span>{{ noteForm.content.length }} characters</span><span>{{ selectedNotebookName }} · {{ noteForm.content.trim() ? noteForm.content.trim().split(/\s+/).length : 0 }} words</span></div>
            </div>
          </ScrollArea>
        </template>
        <Empty v-else class="flex-1"><EmptyHeader><EmptyMedia variant="icon"><HugeiconsIcon :icon="Note01Icon" :size="18" /></EmptyMedia><EmptyTitle>Select a note to begin</EmptyTitle><EmptyDescription>Your ideas are ready when you are.</EmptyDescription></EmptyHeader><EmptyContent><Button @click="newNote"><HugeiconsIcon :icon="NoteAddIcon" data-icon="inline-start" />Create a new note</Button></EmptyContent></Empty>
      </section>
    </div>

    <Alert v-if="error" variant="destructive" class="fixed right-4 bottom-4 w-[min(24rem,calc(100vw-2rem))] shadow-xl"><HugeiconsIcon :icon="Cancel01Icon" :size="16" /><AlertTitle>Something went wrong</AlertTitle><AlertDescription>{{ error }}</AlertDescription><Button variant="ghost" size="icon-xs" class="absolute top-2 right-2" @click="error = null"><HugeiconsIcon :icon="Cancel01Icon" /></Button></Alert>

    <Dialog v-model:open="showManager">
      <DialogContent class="sm:max-w-2xl"><DialogHeader><DialogTitle>Organize your workspace</DialogTitle><DialogDescription>Create, rename, or remove notebooks and tags.</DialogDescription></DialogHeader>
        <Tabs v-model="managerTab"><TabsList><TabsTrigger value="notebooks">Notebooks</TabsTrigger><TabsTrigger value="tags">Tags</TabsTrigger></TabsList>
          <TabsContent value="notebooks" class="flex flex-col gap-4"><form @submit.prevent="saveNotebook"><FieldGroup><Field><FieldLabel for="book-name">Name</FieldLabel><Input id="book-name" v-model.trim="notebookForm.name" required maxlength="100" placeholder="Notebook name" /></Field><Field><FieldLabel for="book-description">Description</FieldLabel><Input id="book-description" v-model.trim="notebookForm.description" maxlength="500" placeholder="Optional description" /></Field><div class="flex gap-2"><Button type="submit" :disabled="busy">{{ editingNotebookId ? 'Update notebook' : 'Add notebook' }}</Button><Button v-if="editingNotebookId" type="button" variant="outline" @click="resetNotebook">Cancel</Button></div></FieldGroup></form><Separator /><ScrollArea class="max-h-64"><div class="flex flex-col gap-2"><div v-for="book in notebooks" :key="book.id" class="flex items-center gap-3 rounded-lg border p-3"><HugeiconsIcon :icon="Folder01Icon" :size="16" /><div class="min-w-0 flex-1"><b class="block truncate text-xs">{{ book.name }}</b><span class="block truncate text-[10px] text-muted-foreground">{{ book.description || `${noteCount('notebook', book.id)} notes` }}</span></div><Button variant="ghost" size="icon-sm" @click="editNotebook(book)"><HugeiconsIcon :icon="Edit02Icon" /></Button><Button variant="destructive" size="icon-sm" @click="requestDelete({ kind: 'notebook', item: book })"><HugeiconsIcon :icon="Delete02Icon" /></Button></div></div></ScrollArea></TabsContent>
          <TabsContent value="tags" class="flex flex-col gap-4"><form @submit.prevent="saveTag"><FieldGroup><Field><FieldLabel for="tag-name">Name</FieldLabel><Input id="tag-name" v-model.trim="tagForm.name" required maxlength="100" placeholder="Tag name" /></Field><div class="flex gap-2"><Button type="submit" :disabled="busy">{{ editingTagId ? 'Update tag' : 'Add tag' }}</Button><Button v-if="editingTagId" type="button" variant="outline" @click="resetTag">Cancel</Button></div></FieldGroup></form><Separator /><ScrollArea class="max-h-64"><div class="flex flex-col gap-2"><div v-for="tag in tags" :key="tag.id" class="flex items-center gap-3 rounded-lg border p-3"><HugeiconsIcon :icon="Tag01Icon" :size="16" /><div class="min-w-0 flex-1"><b class="block truncate text-xs">{{ tag.name }}</b><span class="text-[10px] text-muted-foreground">{{ noteCount('tag', tag.id) }} notes</span></div><Button variant="ghost" size="icon-sm" @click="editTag(tag)"><HugeiconsIcon :icon="Edit02Icon" /></Button><Button variant="destructive" size="icon-sm" @click="requestDelete({ kind: 'tag', item: tag })"><HugeiconsIcon :icon="Delete02Icon" /></Button></div></div></ScrollArea></TabsContent>
        </Tabs>
      </DialogContent>
    </Dialog>

    <AlertDialog v-model:open="deleteDialogOpen"><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>Delete “{{ deleteTargetName() }}”?</AlertDialogTitle><AlertDialogDescription>This action cannot be undone. The selected {{ deleteTarget?.kind }} will be permanently removed.</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>Cancel</AlertDialogCancel><AlertDialogAction variant="destructive" @click="confirmDelete">Delete</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
  </main>
</template>
