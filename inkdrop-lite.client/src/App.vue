<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { HugeiconsIcon } from '@hugeicons/vue'
import {
  Add01Icon,
  Cancel01Icon,
  Menu01Icon,
  Moon02Icon,
  Sun03Icon,
} from '@hugeicons/core-free-icons'

import { ApiError, checkApiHealth } from '@/api/client'
import { attachmentsApi, type AttachmentResponse } from '@/api/attachments'
import { notebooksApi, type NotebookIconType, type NotebookResponse } from '@/api/notebooks'
import { notesApi, NoteStatus, type NoteResponse } from '@/api/notes'
import { tagsApi, type TagResponse } from '@/api/tags'
import { getCurrentAccount, initializeAuth, signIn, signOut } from '@/auth/entra'
import type { MobilePane, NavFilter, Theme } from '@/types/workspace'
import { notebookAndDescendantIds, ROOT_NOTEBOOK } from '@/lib/notebookTree'

import LandingView from '@/components/LandingView.vue'
import ConnectAgentDialog from '@/components/workspace/ConnectAgentDialog.vue'
import NoteEditorPane from '@/components/workspace/NoteEditorPane.vue'
import NotesPane from '@/components/workspace/NotesPane.vue'
import WorkspaceOrganizerDialog from '@/components/workspace/WorkspaceOrganizerDialog.vue'
import WorkspaceSidebar from '@/components/workspace/WorkspaceSidebar.vue'

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
import { Button } from '@/components/ui/button'

type DeleteTarget =
  | { kind: 'note'; item: NoteResponse }
  | { kind: 'notebook'; item: NotebookResponse }
  | { kind: 'tag'; item: TagResponse }
  | { kind: 'attachment'; item: AttachmentResponse }

const storedTheme = localStorage.getItem('inkdrop-lite-theme')
const theme = ref<Theme>(storedTheme === 'dark' ? 'dark' : 'light')
const apiOnline = ref<boolean | null>(null)
const accountName = ref<string | null>(null)
const notes = ref<NoteResponse[]>([])
const notebooks = ref<NotebookResponse[]>([])
const tags = ref<TagResponse[]>([])
const noteAttachments = ref<AttachmentResponse[]>([])
const pendingIconId = ref<string | null>(null)
function storedFlag(key: string) {
  try { return localStorage.getItem(key) === '1' } catch { return false }
}
const sidebarCollapsed = ref(storedFlag('inkdrop-lite-sidebar-collapsed'))
const workspaceColumns = computed(() => `${sidebarCollapsed.value ? '3rem' : '16rem'} 22rem minmax(0,1fr)`)
const busyCount = ref(0)
const error = ref<string | null>(null)
const query = ref('')
const searchIds = ref<Set<string> | null>(null)
let searchTimer: number | undefined
let searchSeq = 0
const selectedNoteId = ref<string | null>(null)
const activeFilter = ref<NavFilter>({ kind: 'all', label: 'All Notes' })
const mobilePane = ref<MobilePane>('notes')
const showManager = ref(false)
const showConnectAgent = ref(false)
const managerTab = ref<'notebooks' | 'tags'>('notebooks')
const editingNotebookId = ref<string | null>(null)
const editingTagId = ref<string | null>(null)
const isNewNote = ref(false)
const templateSourceId = ref<string | null>(null)
const renamingNoteId = ref<string | null>(null)
const renameTitle = ref('')
const deleteTarget = ref<DeleteTarget | null>(null)
const deleteDialogOpen = ref(false)

const busy = computed(() => busyCount.value > 0)
const themeLabel = computed(() => theme.value === 'dark' ? 'Switch to light mode' : 'Switch to dark mode')
const noteForm = reactive({ title: '', content: '', status: NoteStatus.Active, pinned: false, notebookId: '', tagIds: [] as string[] })
const notebookForm = reactive({ name: '', parent: ROOT_NOTEBOOK, order: '', iconType: 'None' as NotebookIconType, iconSvg: '', iconAttachmentId: null as string | null })
const tagForm = reactive({ name: '' })

const filteredNotes = computed(() => {
  let result = notes.value
  const filter = activeFilter.value
  if (filter.kind === 'notebook') {
    const ids = new Set(notebookAndDescendantIds(notebooks.value, filter.id))
    result = result.filter((note) => ids.has(note.notebookId))
  }
  if (filter.kind === 'status') result = result.filter((note) => note.status === filter.id)
  if (filter.kind === 'tag') result = result.filter((note) => note.tagIds?.includes(filter.id))
  const term = query.value.trim().toLocaleLowerCase()
  if (term) {
    // Server results (title, content, notebook, tag) once they arrive; local text match while waiting.
    const ids = searchIds.value
    result = ids ? result.filter((note) => ids.has(note.id)) : result.filter((note) => `${note.title} ${note.content}`.toLocaleLowerCase().includes(term))
  }
  return [...result].sort((a, b) => Number(b.pinned) - Number(a.pinned) || new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime())
})
const selectedNote = computed(() => notes.value.find((note) => note.id === selectedNoteId.value) ?? null)
const templateName = computed(() => notes.value.find((note) => note.id === selectedNote.value?.sourceTemplateId)?.title ?? null)

watch(selectedNoteId, async (id) => {
  noteAttachments.value = []
  if (!id) return
  try {
    const files = await attachmentsApi.forNote(id)
    if (selectedNoteId.value === id) noteAttachments.value = files
  } catch {
    // The list is a convenience; the note itself still works if it cannot be loaded.
  }
})

watch(query, (value) => {
  window.clearTimeout(searchTimer)
  searchIds.value = null
  const term = value.trim()
  const seq = ++searchSeq
  if (!term) return
  searchTimer = window.setTimeout(async () => {
    try {
      const hits = await notesApi.search({ query: term, limit: 100 })
      if (seq === searchSeq) searchIds.value = new Set(hits.map((hit) => hit.id))
    } catch {
      // Keep the local text match if the search request fails.
    }
  }, 250)
})

watch(sidebarCollapsed, (value) => { try { localStorage.setItem('inkdrop-lite-sidebar-collapsed', value ? '1' : '0') } catch { /* storage unavailable */ } })

watch(theme, (value) => {
  document.documentElement.classList.toggle('dark', value === 'dark')
  document.documentElement.style.colorScheme = value
  localStorage.setItem('inkdrop-lite-theme', value)
}, { immediate: true })

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
function openManager(tab: 'notebooks' | 'tags') { managerTab.value = tab; showManager.value = true }
function toggleTheme() { theme.value = theme.value === 'dark' ? 'light' : 'dark' }
function selectNote(note: NoteResponse) {
  selectedNoteId.value = note.id; isNewNote.value = false; templateSourceId.value = null
  Object.assign(noteForm, { title: note.title, content: note.content, status: note.status, pinned: note.pinned, notebookId: note.notebookId, tagIds: [...(note.tagIds ?? [])] })
  mobilePane.value = 'editor'
}
function newNote() {
  const notebookId = activeFilter.value.kind === 'notebook' ? activeFilter.value.id : ''
  if (activeFilter.value.kind !== 'all' && activeFilter.value.kind !== 'notebook') {
    activeFilter.value = { kind: 'all', label: 'All Notes' }
  }
  selectedNoteId.value = null
  isNewNote.value = true
  templateSourceId.value = null
  Object.assign(noteForm, { title: 'Untitled', content: '', status: NoteStatus.Active, pinned: false, notebookId, tagIds: [] })
  mobilePane.value = 'editor'
}
function useAsTemplate(template: NoteResponse) {
  selectedNoteId.value = null
  isNewNote.value = true
  templateSourceId.value = template.id
  Object.assign(noteForm, { title: `${template.title} (copy)`.slice(0, 256), content: template.content, status: NoteStatus.Active, pinned: false, notebookId: template.notebookId, tagIds: [...template.tagIds] })
  mobilePane.value = 'editor'
}
async function saveNote() { await execute(async () => {
  const notebookId = noteForm.notebookId || notebooks.value[0]?.id
  if (!notebookId) throw new Error('Create a notebook first — every note must belong to one.')
  const payload = { ...noteForm, title: noteForm.title.trim() || 'Untitled', notebookId }
  if (isNewNote.value) {
    const created = await notesApi.create({ ...payload, sourceTemplateId: templateSourceId.value })
    templateSourceId.value = null
    notes.value = await notesApi.getAll()
    selectNote(notes.value.find((note) => note.id === created.id) ?? created)
    return
  }
  if (!selectedNoteId.value) return
  await notesApi.update(selectedNoteId.value, payload)
  notes.value = await notesApi.getAll()
  const saved = notes.value.find((note) => note.id === selectedNoteId.value)
  if (saved) selectNote(saved)
}) }
async function uploadAttachments(files: File[]) {
  const noteId = selectedNoteId.value
  if (!noteId) return
  await execute(async () => {
    for (const file of files) {
      const uploaded = await attachmentsApi.upload(file)
      try {
        await attachmentsApi.attachToNote(noteId, uploaded.id)
      } catch (cause) {
        await attachmentsApi.delete(uploaded.id).catch(() => undefined)
        throw cause
      }
    }
    if (selectedNoteId.value === noteId) noteAttachments.value = await attachmentsApi.forNote(noteId)
  })
}
async function downloadAttachment(file: AttachmentResponse) {
  await execute(async () => {
    const url = URL.createObjectURL(await attachmentsApi.content(file.id))
    const link = document.createElement('a')
    link.href = url
    link.download = file.name
    link.click()
    window.setTimeout(() => URL.revokeObjectURL(url), 10_000)
  })
}
async function uploadNotebookIcon(file: File) {
  await execute(async () => {
    const uploaded = await attachmentsApi.upload(file)
    if (pendingIconId.value) await attachmentsApi.delete(pendingIconId.value).catch(() => undefined)
    pendingIconId.value = uploaded.id
    notebookForm.iconAttachmentId = uploaded.id
  })
}
function startRename(note: NoteResponse) {
  if (selectedNoteId.value !== note.id) selectNote(note)
  renameTitle.value = note.title
  renamingNoteId.value = note.id
}
function cancelRename() { renamingNoteId.value = null; renameTitle.value = '' }
async function commitRename(note: NoteResponse) {
  if (renamingNoteId.value !== note.id) return
  const title = renameTitle.value.trim() || 'Untitled'
  const isSelected = selectedNoteId.value === note.id
  cancelRename()
  await execute(async () => {
    await notesApi.update(note.id, {
      title,
      content: isSelected ? noteForm.content : note.content,
      status: isSelected ? noteForm.status : note.status,
      pinned: isSelected ? noteForm.pinned : note.pinned,
      notebookId: isSelected ? noteForm.notebookId || note.notebookId : note.notebookId,
      tagIds: isSelected ? noteForm.tagIds : note.tagIds,
    })
    notes.value = await notesApi.getAll()
    if (isSelected) noteForm.title = title
  })
}
async function saveNotebook() { await execute(async () => {
  const name = notebookForm.name.trim()
  const orderValue = notebookForm.order.trim()
  const iconSvg = notebookForm.iconSvg.trim()
  const request = {
    name,
    parentNotebookId: notebookForm.parent === ROOT_NOTEBOOK ? null : notebookForm.parent,
    order: orderValue === '' ? null : Number.parseInt(orderValue, 10),
    iconType: notebookForm.iconType === 'Svg' && !iconSvg ? 'None' as const : notebookForm.iconType,
    iconSvg: notebookForm.iconType === 'Svg' ? iconSvg : null,
    iconAttachmentId: notebookForm.iconType === 'Attachment' ? notebookForm.iconAttachmentId : null,
  }
  if (request.order !== null && !Number.isInteger(request.order)) throw new Error('Order must be a whole number.')
  const previousIcon = notebooks.value.find((book) => book.id === editingNotebookId.value)?.iconAttachmentId ?? null
  if (editingNotebookId.value) await notebooksApi.update(editingNotebookId.value, request)
  else await notebooksApi.create(request)
  pendingIconId.value = null
  // The old icon file is no longer referenced once the notebook points elsewhere.
  if (previousIcon && previousIcon !== request.iconAttachmentId) await attachmentsApi.delete(previousIcon).catch(() => undefined)
  notebooks.value = await notebooksApi.getAll()
  resetNotebook()
}) }
function editNotebook(book: NotebookResponse) {
  editingNotebookId.value = book.id
  Object.assign(notebookForm, { name: book.name, parent: book.parentNotebookId ?? ROOT_NOTEBOOK, order: book.order === null ? '' : String(book.order), iconType: book.iconType, iconSvg: book.iconSvg ?? '', iconAttachmentId: book.iconAttachmentId })
}
function resetNotebook() {
  // An icon uploaded but never saved would otherwise stay on disk unreferenced.
  if (pendingIconId.value) void attachmentsApi.delete(pendingIconId.value).catch(() => undefined)
  pendingIconId.value = null
  editingNotebookId.value = null
  Object.assign(notebookForm, { name: '', parent: ROOT_NOTEBOOK, order: '', iconType: 'None', iconSvg: '', iconAttachmentId: null })
}
async function saveTag() { await execute(async () => { const name = tagForm.name.trim(); const existing = tags.value.find((tag) => tag.id === editingTagId.value); if (existing) await tagsApi.update(existing.id, { name, color: existing.color }); else await tagsApi.create({ name, color: 'Default' }); tags.value = await tagsApi.getAll(); resetTag() }) }
function editTag(tag: TagResponse) { editingTagId.value = tag.id; tagForm.name = tag.name }
function resetTag() { editingTagId.value = null; tagForm.name = '' }
function requestDelete(target: DeleteTarget) { deleteTarget.value = target; deleteDialogOpen.value = true }
function cancelDelete() { deleteDialogOpen.value = false; deleteTarget.value = null }
async function confirmDelete() {
  const target = deleteTarget.value
  if (!target) return
  await execute(async () => {
    if (target.kind === 'note') {
      await notesApi.delete(target.item.id)
      notes.value = notes.value.filter((item) => item.id !== target.item.id)
      if (selectedNoteId.value === target.item.id) {
        selectedNoteId.value = null
        isNewNote.value = false
        if (renamingNoteId.value === target.item.id) cancelRename()
        Object.assign(noteForm, { title: '', content: '', status: NoteStatus.Active, pinned: false, notebookId: '', tagIds: [] })
        const nextNote = filteredNotes.value[0]
        if (nextNote) selectNote(nextNote)
        else mobilePane.value = 'notes'
      }
    } else if (target.kind === 'attachment') {
      await attachmentsApi.delete(target.item.id)
      noteAttachments.value = noteAttachments.value.filter((file) => file.id !== target.item.id)
    } else if (target.kind === 'notebook') {
      const iconId = target.item.iconAttachmentId
      await notebooksApi.delete(target.item.id)
      if (iconId) await attachmentsApi.delete(iconId).catch(() => undefined)
      await loadAll()
    } else {
      await tagsApi.delete(target.item.id)
      tags.value = await tagsApi.getAll()
    }
    deleteTarget.value = null
    deleteDialogOpen.value = false
  })
}
function deleteTargetName() { const target = deleteTarget.value; if (!target) return ''; return target.kind === 'note' ? target.item.title : target.item.name }
async function execute(action: () => Promise<void>, clearError = true) { busyCount.value++; if (clearError) error.value = null; try { await action() } catch (cause) { error.value = cause instanceof ApiError || cause instanceof Error ? cause.message : 'Something went wrong.' } finally { busyCount.value-- } }
</script>

<template>
  <LandingView v-if="!accountName" :api-online="apiOnline" :busy="busy" :theme="theme" :theme-label="themeLabel" @connect="connect" @toggle-theme="toggleTheme" />

  <main v-else class="h-dvh overflow-hidden bg-background text-foreground">
    <header class="flex h-12 items-center justify-between border-b px-3 lg:hidden">
      <Button variant="ghost" size="icon" aria-label="Open library" @click="mobilePane = 'library'"><HugeiconsIcon :icon="Menu01Icon" /></Button>
      <span class="text-xs font-medium">{{ activeFilter.label }}</span>
      <div class="flex items-center gap-1">
        <Button variant="ghost" size="icon" :aria-label="themeLabel" :title="themeLabel" @click="toggleTheme"><HugeiconsIcon :icon="theme === 'dark' ? Sun03Icon : Moon02Icon" /></Button>
        <Button variant="ghost" size="icon" aria-label="New note" :disabled="busy" @click="newNote"><HugeiconsIcon :icon="Add01Icon" /></Button>
      </div>
    </header>

    <div class="grid h-[calc(100dvh-3rem)] lg:h-dvh lg:grid-cols-(--workspace-columns)" :style="{ '--workspace-columns': workspaceColumns }">
      <WorkspaceSidebar
        :account-name="accountName"
        :active-filter="activeFilter"
        :busy="busy"
        :collapsed="sidebarCollapsed"
        :mobile-pane="mobilePane"
        :notebooks="notebooks"
        :notes="notes"
        :tags="tags"
        :theme="theme"
        :theme-label="themeLabel"
        @collapse="sidebarCollapsed = true"
        @expand="sidebarCollapsed = false"
        @connect-agent="showConnectAgent = true"
        @create="newNote"
        @filter="setFilter"
        @manage="openManager"
        @sign-out="disconnect"
        @toggle-theme="toggleTheme"
      />

      <NotesPane
        v-model:query="query"
        v-model:rename-title="renameTitle"
        :active-filter="activeFilter"
        :busy="busy"
        :mobile-pane="mobilePane"
        :notes="filteredNotes"
        :renaming-note-id="renamingNoteId"
        :selected-note-id="selectedNoteId"
        :tags="tags"
        @cancel-rename="cancelRename"
        @commit-rename="commitRename"
        @create="newNote"
        @open-library="mobilePane = 'library'"
        @select="selectNote"
        @start-rename="startRename"
      />

      <NoteEditorPane
        v-model:content="noteForm.content"
        v-model:notebook-id="noteForm.notebookId"
        v-model:pinned="noteForm.pinned"
        v-model:status="noteForm.status"
        v-model:tag-ids="noteForm.tagIds"
        :attachments="noteAttachments"
        :busy="busy"
        :fresh="isNewNote"
        :mobile-pane="mobilePane"
        :notebooks="notebooks"
        :note="selectedNote"
        :tags="tags"
        :template-name="templateName"
        @back="mobilePane = 'notes'"
        @create="newNote"
        @delete="(note) => requestDelete({ kind: 'note', item: note })"
        @delete-attachment="(file) => requestDelete({ kind: 'attachment', item: file })"
        @download-attachment="downloadAttachment"
        @save="saveNote"
        @upload-attachments="uploadAttachments"
        @use-as-template="useAsTemplate"
      />
    </div>

    <Alert v-if="error" variant="destructive" class="fixed right-4 bottom-4 w-[min(24rem,calc(100vw-2rem))] shadow-xl"><HugeiconsIcon :icon="Cancel01Icon" :size="16" /><AlertTitle>Something went wrong</AlertTitle><AlertDescription>{{ error }}</AlertDescription><Button variant="ghost" size="icon-xs" class="absolute top-2 right-2" @click="error = null"><HugeiconsIcon :icon="Cancel01Icon" /></Button></Alert>

    <WorkspaceOrganizerDialog
      v-model:open="showManager"
      v-model:tab="managerTab"
      v-model:notebook-name="notebookForm.name"
      v-model:notebook-parent="notebookForm.parent"
      v-model:notebook-order="notebookForm.order"
      v-model:notebook-icon-type="notebookForm.iconType"
      v-model:notebook-icon-svg="notebookForm.iconSvg"
      v-model:notebook-icon-attachment-id="notebookForm.iconAttachmentId"
      v-model:tag-name="tagForm.name"
      :busy="busy"
      :editing-notebook-id="editingNotebookId"
      :editing-tag-id="editingTagId"
      :notebooks="notebooks"
      :notes="notes"
      :tags="tags"
      @delete-notebook="(notebook) => requestDelete({ kind: 'notebook', item: notebook })"
      @delete-tag="(tag) => requestDelete({ kind: 'tag', item: tag })"
      @edit-notebook="editNotebook"
      @edit-tag="editTag"
      @reset-notebook="resetNotebook"
      @reset-tag="resetTag"
      @save-notebook="saveNotebook"
      @save-tag="saveTag"
      @upload-icon="uploadNotebookIcon"
    />

    <ConnectAgentDialog v-model:open="showConnectAgent" />

    <AlertDialog v-model:open="deleteDialogOpen"><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>Delete “{{ deleteTargetName() }}”?</AlertDialogTitle><AlertDialogDescription>This action cannot be undone. The selected {{ deleteTarget?.kind }} will be permanently removed.</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel @click="cancelDelete">Cancel</AlertDialogCancel><AlertDialogAction variant="destructive" @click="confirmDelete">Delete</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
  </main>
</template>
