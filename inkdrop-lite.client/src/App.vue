<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'

import { ApiError, checkApiHealth } from '@/api/client'
import { notebooksApi, type NotebookResponse } from '@/api/notebooks'
import { notesApi, NoteStatus, type NoteResponse } from '@/api/notes'
import { tagsApi, type TagResponse } from '@/api/tags'
import { getCurrentAccount, initializeAuth, signIn, signOut } from '@/auth/entra'

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE'
type LogEntry = { id: number; method: HttpMethod; path: string; status: number; time: string }

const apiOnline = ref<boolean | null>(null)
const accountName = ref<string | null>(null)
const notes = ref<NoteResponse[]>([])
const notebooks = ref<NotebookResponse[]>([])
const tags = ref<TagResponse[]>([])
const busyCount = ref(0)
const error = ref<string | null>(null)
const responseBody = ref('Select GET on a resource to inspect its response.')
const requestLog = ref<LogEntry[]>([])
const activeResource = ref<'notes' | 'notebooks' | 'tags'>('notes')
const editingNoteId = ref<string | null>(null)
const editingNotebookId = ref<string | null>(null)
const editingTagId = ref<string | null>(null)
const busy = computed(() => busyCount.value > 0)

const noteForm = reactive<{
  title: string
  content: string
  status: NoteStatus
  notebookId: string
  tagIds: string[]
}>({ title: '', content: '', status: NoteStatus.Active, notebookId: '', tagIds: [] })
const notebookForm = reactive({ name: '', description: '' })
const tagForm = reactive({ name: '' })
const noteStatuses = [
  { value: NoteStatus.Active, label: 'Active' },
  { value: NoteStatus.OnHold, label: 'On hold' },
  { value: NoteStatus.Completed, label: 'Completed' },
  { value: NoteStatus.Dropped, label: 'Dropped' },
]

onMounted(async () => {
  await execute(async () => {
    await initializeAuth()
    accountName.value = getCurrentAccount()?.username ?? null
    apiOnline.value = await checkApiHealth()
    if (accountName.value) await loadAll()
  }, false)
})

async function connect() {
  await execute(async () => {
    accountName.value = (await signIn()).username
    await loadAll()
  }, false)
}

async function disconnect() {
  await execute(async () => {
    await signOut()
    accountName.value = null
    notes.value = []
    notebooks.value = []
    tags.value = []
  }, false)
}

async function loadAll() {
  const [noteData, notebookData, tagData] = await Promise.all([
    request('GET', '/api/notes', () => notesApi.getAll()),
    request('GET', '/api/notebooks', () => notebooksApi.getAll()),
    request('GET', '/api/tags', () => tagsApi.getAll()),
  ])
  notes.value = noteData
  notebooks.value = notebookData
  tags.value = tagData
}

async function saveNote() {
  await execute(async () => {
    const payload = { ...noteForm, notebookId: noteForm.notebookId || null }
    if (editingNoteId.value) {
      await request('PUT', `/api/notes/${editingNoteId.value}`, () => notesApi.update(editingNoteId.value!, payload), 204)
    } else {
      await request('POST', '/api/notes', () => notesApi.create(payload), 201)
    }
    resetNote()
    notes.value = await request('GET', '/api/notes', () => notesApi.getAll())
  })
}

function editNote(note: NoteResponse) {
  editingNoteId.value = note.id
  Object.assign(noteForm, {
    title: note.title,
    content: note.content,
    status: note.status,
    notebookId: note.notebookId ?? '',
    tagIds: [...(note.tagIds ?? [])],
  })
}

function resetNote() {
  editingNoteId.value = null
  Object.assign(noteForm, {
    title: '',
    content: '',
    status: NoteStatus.Active,
    notebookId: '',
    tagIds: [],
  })
}

function tagsFor(note: NoteResponse): TagResponse[] {
  return tags.value.filter((tag) => (note.tagIds ?? []).includes(tag.id))
}

async function deleteNote(note: NoteResponse) {
  if (!confirm(`Delete note "${note.title}"?`)) return
  await execute(async () => {
    await request('DELETE', `/api/notes/${note.id}`, () => notesApi.delete(note.id), 204)
    notes.value = notes.value.filter((item) => item.id !== note.id)
  })
}

async function saveNotebook() {
  await execute(async () => {
    const payload = { name: notebookForm.name, description: notebookForm.description || null }
    if (editingNotebookId.value) {
      await request('PUT', `/api/notebooks/${editingNotebookId.value}`, () => notebooksApi.update(editingNotebookId.value!, payload), 204)
    } else {
      await request('POST', '/api/notebooks', () => notebooksApi.create(payload), 201)
    }
    resetNotebook()
    notebooks.value = await request('GET', '/api/notebooks', () => notebooksApi.getAll())
  })
}

function editNotebook(notebook: NotebookResponse) {
  editingNotebookId.value = notebook.id
  Object.assign(notebookForm, { name: notebook.name, description: notebook.description ?? '' })
}

function resetNotebook() {
  editingNotebookId.value = null
  Object.assign(notebookForm, { name: '', description: '' })
}

async function deleteNotebook(notebook: NotebookResponse) {
  if (!confirm(`Delete notebook "${notebook.name}"?`)) return
  await execute(async () => {
    await request('DELETE', `/api/notebooks/${notebook.id}`, () => notebooksApi.delete(notebook.id), 204)
    notebooks.value = notebooks.value.filter((item) => item.id !== notebook.id)
  })
}

async function saveTag() {
  await execute(async () => {
    const payload = { name: tagForm.name }
    if (editingTagId.value) {
      await request('PUT', `/api/tags/${editingTagId.value}`, () => tagsApi.update(editingTagId.value!, payload), 204)
    } else {
      await request('POST', '/api/tags', () => tagsApi.create(payload), 201)
    }
    resetTag()
    tags.value = await request('GET', '/api/tags', () => tagsApi.getAll())
  })
}

function editTag(tag: TagResponse) {
  editingTagId.value = tag.id
  tagForm.name = tag.name
}

function resetTag() {
  editingTagId.value = null
  tagForm.name = ''
}

async function deleteTag(tag: TagResponse) {
  if (!confirm(`Delete tag "${tag.name}"?`)) return
  await execute(async () => {
    await request('DELETE', `/api/tags/${tag.id}`, () => tagsApi.delete(tag.id), 204)
    tags.value = tags.value.filter((item) => item.id !== tag.id)
  })
}

async function inspect(path: string, loader: () => Promise<unknown>) {
  await execute(async () => {
    responseBody.value = JSON.stringify(await request('GET', path, loader), null, 2)
  })
}

async function request<T>(method: HttpMethod, path: string, action: () => Promise<T>, successStatus = 200): Promise<T> {
  try {
    const result = await action()
    addLog(method, path, successStatus)
    return result
  } catch (cause) {
    addLog(method, path, cause instanceof ApiError ? cause.status : 0)
    throw cause
  }
}

function addLog(method: HttpMethod, path: string, status: number) {
  requestLog.value.unshift({ id: Date.now() + Math.random(), method, path, status, time: new Date().toLocaleTimeString() })
  requestLog.value = requestLog.value.slice(0, 20)
}

async function execute(action: () => Promise<void>, clearError = true) {
  busyCount.value++
  if (clearError) error.value = null
  try {
    await action()
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : 'An unexpected error occurred.'
  } finally {
    busyCount.value--
  }
}
</script>

<template>
  <main class="shell">
    <header class="topbar">
      <div>
        <p class="eyebrow">Inkdrop Lite</p>
        <h1>API Playground</h1>
      </div>
      <div class="connection">
        <span :class="['dot', apiOnline ? 'online' : 'offline']"></span>
        API {{ apiOnline === null ? 'checking' : apiOnline ? 'online' : 'offline' }}
        <span v-if="accountName">{{ accountName }}</span>
        <button v-if="accountName" class="secondary" :disabled="busy" @click="execute(loadAll)">Refresh all</button>
        <button v-if="accountName" class="secondary" :disabled="busy" @click="disconnect">Sign out</button>
      </div>
    </header>

    <section v-if="!accountName" class="login card">
      <h2>Connect to test protected endpoints</h2>
      <p>Sign in with Entra ID. Every action below sends a real authenticated HTTP request.</p>
      <button :disabled="busy" @click="connect">{{ busy ? 'Connecting...' : 'Sign in' }}</button>
    </section>

    <template v-else>
      <p v-if="error" class="error card">{{ error }}</p>
      <nav class="resource-tabs" aria-label="API resources">
        <button :class="{ active: activeResource === 'notes' }" @click="activeResource = 'notes'">
          Notes <span>{{ notes.length }}</span>
        </button>
        <button :class="{ active: activeResource === 'notebooks' }" @click="activeResource = 'notebooks'">
          Notebooks <span>{{ notebooks.length }}</span>
        </button>
        <button :class="{ active: activeResource === 'tags' }" @click="activeResource = 'tags'">
          Tags <span>{{ tags.length }}</span>
        </button>
      </nav>

      <section class="workspace">
        <article v-if="activeResource === 'notes'" class="card resource-panel">
          <div class="section-title"><div><p class="route">/api/notes</p><h2>Notes CRUD</h2></div><span>GET POST PUT DELETE</span></div>
          <form class="form" @submit.prevent="saveNote">
            <input v-model.trim="noteForm.title" required maxlength="200" placeholder="Note title" />
            <select v-model.number="noteForm.status">
              <option v-for="status in noteStatuses" :key="status.value" :value="status.value">{{ status.label }}</option>
            </select>
            <select v-model="noteForm.notebookId">
              <option value="">No notebook</option>
              <option v-for="notebook in notebooks" :key="notebook.id" :value="notebook.id">{{ notebook.name }}</option>
            </select>
            <fieldset class="tag-picker">
              <legend>Tags</legend>
              <div v-if="tags.length" class="tag-options">
                <label v-for="tag in tags" :key="tag.id" class="tag-option">
                  <input v-model="noteForm.tagIds" type="checkbox" :value="tag.id" />
                  {{ tag.name }}
                </label>
              </div>
              <p v-else class="empty">Create a tag from the Tags tab first.</p>
            </fieldset>
            <textarea v-model="noteForm.content" rows="7" placeholder="Large Markdown content is supported"></textarea>
            <div class="actions"><button :disabled="busy">{{ editingNoteId ? 'PUT update' : 'POST create' }}</button><button v-if="editingNoteId" type="button" class="secondary" @click="resetNote">Cancel</button></div>
          </form>
          <div class="rows">
            <div v-for="note in notes" :key="note.id" class="row">
              <div>
                <strong>{{ note.title }}</strong>
                <small>{{ noteStatuses[note.status]?.label }} - {{ note.id }}</small>
                <div v-if="tagsFor(note).length" class="tag-badges">
                  <span v-for="tag in tagsFor(note)" :key="tag.id">{{ tag.name }}</span>
                </div>
              </div>
              <div class="row-actions"><button class="tiny" @click="inspect(`/api/notes/${note.id}`, () => notesApi.getById(note.id))">GET</button><button class="tiny secondary" @click="editNote(note)">Edit</button><button class="tiny danger" @click="deleteNote(note)">Delete</button></div>
            </div>
            <p v-if="!notes.length" class="empty">No notes yet.</p>
          </div>
        </article>

        <article v-else-if="activeResource === 'notebooks'" class="card resource-panel">
            <div class="section-title"><div><p class="route">/api/notebooks</p><h2>Notebooks</h2></div></div>
            <form class="compact-form" @submit.prevent="saveNotebook"><input v-model.trim="notebookForm.name" required maxlength="100" placeholder="Name" /><input v-model.trim="notebookForm.description" maxlength="500" placeholder="Description" /><button :disabled="busy">{{ editingNotebookId ? 'PUT' : 'POST' }}</button><button v-if="editingNotebookId" type="button" class="secondary" @click="resetNotebook">Cancel</button></form>
            <div v-for="notebook in notebooks" :key="notebook.id" class="row"><div><strong>{{ notebook.name }}</strong><small>{{ notebook.id }}</small></div><div class="row-actions"><button class="tiny" @click="inspect(`/api/notebooks/${notebook.id}`, () => notebooksApi.getById(notebook.id))">GET</button><button class="tiny secondary" @click="editNotebook(notebook)">Edit</button><button class="tiny danger" @click="deleteNotebook(notebook)">Delete</button></div></div>
            <p v-if="!notebooks.length" class="empty">No notebooks yet.</p>
        </article>

        <article v-else class="card resource-panel">
            <div class="section-title"><div><p class="route">/api/tags</p><h2>Tags</h2></div></div>
            <form class="compact-form tag-form" @submit.prevent="saveTag"><input v-model.trim="tagForm.name" required maxlength="100" placeholder="Tag name" /><button :disabled="busy">{{ editingTagId ? 'PUT' : 'POST' }}</button><button v-if="editingTagId" type="button" class="secondary" @click="resetTag">Cancel</button></form>
            <div v-for="tag in tags" :key="tag.id" class="row"><div><strong>{{ tag.name }}</strong><small>{{ tag.id }}</small></div><div class="row-actions"><button class="tiny" @click="inspect(`/api/tags/${tag.id}`, () => tagsApi.getById(tag.id))">GET</button><button class="tiny secondary" @click="editTag(tag)">Edit</button><button class="tiny danger" @click="deleteTag(tag)">Delete</button></div></div>
            <p v-if="!tags.length" class="empty">No tags yet.</p>
        </article>
      </section>

      <details class="card diagnostics">
        <summary>Developer output <span>{{ requestLog.length }} requests</span></summary>
        <div class="diagnostic-grid">
          <section><h2>Response inspector</h2><pre>{{ responseBody }}</pre></section>
          <section><div class="section-title"><h2>Request log</h2><button class="tiny secondary" @click="requestLog = []">Clear</button></div><div class="log"><div v-for="entry in requestLog" :key="entry.id" class="log-row"><code :class="entry.method.toLowerCase()">{{ entry.method }}</code><span>{{ entry.path }}</span><b :class="entry.status >= 200 && entry.status < 300 ? 'ok' : 'bad'">{{ entry.status || 'ERR' }}</b><time>{{ entry.time }}</time></div><p v-if="!requestLog.length" class="empty">Requests will appear here.</p></div></section>
        </div>
      </details>
    </template>
  </main>
</template>

<style scoped>
:global(html),
:global(body) {
  width: 100%;
  min-width: 320px;
  margin: 0;
}

:global(body) {
  display: block !important;
  place-items: initial !important;
}

:global(#app) {
  display: block !important;
  width: 100% !important;
  max-width: none !important;
  margin: 0 !important;
  padding: 0 !important;
}

.shell {
  width: 100%;
  max-width: 1180px;
  margin: 0 auto;
  padding: 32px 24px 56px;
}

.topbar,
.connection,
.section-title,
.actions,
.row-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.topbar {
  justify-content: space-between;
  padding-bottom: 24px;
  border-bottom: 1px solid var(--color-border);
}

.eyebrow,
.route {
  margin: 0;
  color: #42b883;
  font-size: 12px;
  font-weight: 750;
  letter-spacing: .11em;
  text-transform: uppercase;
}

h1,
h2,
p {
  margin-top: 0;
}

h1 {
  margin: 3px 0 0;
  color: var(--color-heading);
  font-size: 32px;
  font-weight: 650;
  line-height: 1.2;
}

h2 {
  margin: 2px 0;
  color: var(--color-heading);
  font-size: 18px;
  font-weight: 620;
}

.connection {
  flex-wrap: wrap;
  justify-content: flex-end;
  color: #aaa;
  font-size: 13px;
}

.connection > span:last-of-type {
  padding-left: 10px;
  border-left: 1px solid var(--color-border);
}

.dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #777;
}

.dot.online {
  background: #42b883;
  box-shadow: 0 0 10px #42b88388;
}

.dot.offline {
  background: #e35d6a;
}

.card {
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-background-soft);
}

.login {
  max-width: 560px;
  margin: 12vh auto;
  padding: 28px;
}

.login p {
  color: #aaa;
}

.resource-tabs {
  display: flex;
  gap: 8px;
  margin: 24px 0 12px;
}

.resource-tabs button {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--color-text);
  border: 1px solid var(--color-border);
  background: transparent;
}

.resource-tabs button.active {
  color: #fff;
  border-color: #32795e;
  background: #245d48;
}

.resource-tabs span {
  min-width: 24px;
  padding: 1px 7px;
  border-radius: 999px;
  background: rgb(255 255 255 / 9%);
  text-align: center;
}

.workspace {
  width: 100%;
}

.resource-panel {
  min-height: 380px;
  padding: 24px;
}

.section-title {
  justify-content: space-between;
  margin-bottom: 20px;
}

.section-title > span {
  color: #777;
  font: 12px ui-monospace, monospace;
}

.form {
  display: grid;
  grid-template-columns: minmax(240px, 2fr) minmax(140px, .7fr) minmax(180px, 1fr);
  gap: 12px;
}

.form textarea,
.form .actions {
  grid-column: 1 / -1;
}

.tag-picker {
  grid-column: 1 / -1;
  margin: 0;
  padding: 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
}

.tag-picker legend {
  padding: 0 6px;
  color: #999;
  font-size: 12px;
}

.tag-options,
.tag-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.tag-option {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  padding: 6px 10px;
  border-radius: 999px;
  background: var(--color-background-mute);
  cursor: pointer;
  font-size: 13px;
}

.tag-option input {
  width: auto;
  margin: 0;
  accent-color: #42b883;
}

.tag-badges {
  margin-top: 6px;
}

.tag-badges span {
  padding: 2px 8px;
  color: #71d3aa;
  border: 1px solid #32795e;
  border-radius: 999px;
  font-size: 11px;
}

.compact-form {
  display: grid;
  grid-template-columns: minmax(220px, 1fr) minmax(280px, 2fr) auto auto;
  gap: 10px;
  margin-bottom: 18px;
}

.tag-form {
  grid-template-columns: minmax(220px, 1fr) auto auto;
}

input,
select,
textarea {
  width: 100%;
  min-width: 0;
  padding: 11px 12px;
  color: var(--color-text);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  outline: none;
  background: var(--color-background);
  font: inherit;
  color-scheme: dark;
}

input:focus,
select:focus,
textarea:focus {
  border-color: #42b883;
  box-shadow: 0 0 0 3px rgb(66 184 131 / 12%);
}

textarea {
  min-height: 160px;
  resize: vertical;
}

button {
  padding: 10px 14px;
  color: white;
  border: 0;
  border-radius: 8px;
  background: #287c5a;
  cursor: pointer;
  font: inherit;
}

button:hover {
  filter: brightness(1.12);
}

button:disabled {
  opacity: .5;
  cursor: wait;
}

.secondary {
  color: var(--color-text);
  background: var(--color-background-mute);
}

.danger {
  color: #ffb9bf;
  background: #682a32;
}

.tiny {
  padding: 6px 9px;
  font-size: 12px;
}

.rows {
  margin-top: 18px;
}

.row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 12px 0;
  border-top: 1px solid var(--color-border);
}

.row > div:first-child {
  min-width: 0;
}

.row strong,
.row small {
  display: block;
}

.row strong {
  font-weight: 600;
}

.row small {
  overflow: hidden;
  color: #888;
  font: 11px ui-monospace, monospace;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.row-actions {
  flex-shrink: 0;
}

.diagnostics {
  margin-top: 12px;
  padding: 0;
  overflow: hidden;
}

.diagnostics summary {
  display: flex;
  justify-content: space-between;
  padding: 14px 18px;
  cursor: pointer;
  font-weight: 600;
}

.diagnostics summary span {
  color: #888;
  font-size: 12px;
  font-weight: normal;
}

.diagnostic-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
  padding: 20px;
  border-top: 1px solid var(--color-border);
}

.diagnostics pre {
  max-height: 280px;
  overflow: auto;
  margin: 12px 0 0;
  padding: 14px;
  border-radius: 8px;
  background: #101312;
  font-size: 12px;
  white-space: pre-wrap;
}

.log {
  max-height: 280px;
  overflow: auto;
}

.log-row {
  display: grid;
  grid-template-columns: 50px 1fr 42px auto;
  align-items: center;
  gap: 10px;
  padding: 9px 0;
  border-top: 1px solid var(--color-border);
  font-size: 12px;
}

.log-row span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.log-row time,
.empty {
  color: #888;
}

.log-row code {
  font-weight: 800;
}

.get { color: #67b7ff; }
.post, .ok { color: #42b883; }
.put { color: #f0b35b; }
.delete, .bad, .error { color: #ff7883; }
.error { margin-top: 18px; padding: 12px 16px; }
.empty { font-size: 13px; }

@media (max-width: 760px) {
  .shell { padding: 20px 14px 40px; }
  .topbar { align-items: flex-start; flex-direction: column; }
  .connection { justify-content: flex-start; }
  .resource-tabs { overflow-x: auto; }
  .form, .compact-form, .tag-form, .diagnostic-grid { grid-template-columns: 1fr; }
  .form > * { grid-column: 1 !important; }
  .row { align-items: flex-start; flex-direction: column; }
  .row-actions { flex-wrap: wrap; }
}
</style>
