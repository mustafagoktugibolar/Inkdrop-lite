<script setup lang="ts">
import { computed } from 'vue'
import { HugeiconsIcon } from '@hugeicons/vue'
import { ArrowLeft01Icon, Delete02Icon, Edit02Icon, Layout2ColumnIcon, Note01Icon, NoteAddIcon, ViewIcon } from '@hugeicons/core-free-icons'

import type { NotebookResponse } from '@/api/notebooks'
import { NoteStatus, type NoteResponse } from '@/api/notes'
import type { TagResponse } from '@/api/tags'
import type { MobilePane } from '@/types/workspace'
import { cn } from '@/lib/utils'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { Field, FieldGroup, FieldLabel, FieldLegend, FieldSet } from '@/components/ui/field'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'

const props = defineProps<{
  busy: boolean
  fresh: boolean
  mobilePane: MobilePane
  notebooks: NotebookResponse[]
  note: NoteResponse | null
  tags: TagResponse[]
}>()

const content = defineModel<string>('content', { required: true })
const status = defineModel<NoteStatus>('status', { required: true })
const notebookId = defineModel<string>('notebookId', { required: true })
const tagIds = defineModel<string[]>('tagIds', { required: true })

const emit = defineEmits<{
  back: []
  create: []
  delete: [note: NoteResponse]
  save: []
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
const selectedNotebookName = computed(() => props.notebooks.find((book) => book.id === notebookId.value)?.name ?? 'No notebook')
const selectedTags = computed(() => props.tags.filter((tag) => tagIds.value.includes(tag.id)))
const wordCount = computed(() => content.value.trim() ? content.value.trim().split(/\s+/).length : 0)

function toggleTag(id: string) {
  tagIds.value = tagIds.value.includes(id)
    ? tagIds.value.filter((tagId) => tagId !== id)
    : [...tagIds.value, id]
}
</script>

<template>
  <section :class="cn('min-h-0 flex-col bg-background lg:flex', mobilePane === 'editor' ? 'flex' : 'hidden')">
    <header class="flex h-12 items-center justify-between border-b px-3"><Button class="lg:hidden" variant="ghost" size="sm" @click="emit('back')"><HugeiconsIcon :icon="ArrowLeft01Icon" data-icon="inline-start" />Notes</Button><div class="hidden items-center gap-1 lg:flex"><Button variant="secondary" size="icon-sm"><HugeiconsIcon :icon="Edit02Icon" /></Button><Button variant="ghost" size="icon-sm"><HugeiconsIcon :icon="Layout2ColumnIcon" /></Button><Button variant="ghost" size="icon-sm"><HugeiconsIcon :icon="ViewIcon" /></Button></div><div class="flex items-center gap-2"><span v-if="busy" class="text-[10px] text-muted-foreground">Saving…</span><Button v-if="note" variant="ghost" size="icon-sm" aria-label="Delete note" @click="emit('delete', note)"><HugeiconsIcon :icon="Delete02Icon" /></Button><Button v-if="note || fresh" size="sm" :disabled="busy" @click="emit('save')">{{ fresh ? 'Create' : 'Save' }}</Button></div></header>

    <template v-if="note || fresh">
      <ScrollArea class="flex-1">
        <div class="mx-auto flex w-full max-w-4xl flex-col gap-5 p-5 sm:p-8">
          <FieldGroup>
            <div v-if="!fresh" class="grid gap-3 sm:grid-cols-2">
              <Field><FieldLabel>Notebook</FieldLabel><Select v-model="notebookModel"><SelectTrigger class="w-full"><SelectValue placeholder="Select a notebook" /></SelectTrigger><SelectContent><SelectGroup><SelectItem v-for="book in notebooks" :key="book.id" :value="book.id">{{ book.name }}</SelectItem></SelectGroup></SelectContent></Select></Field>
              <Field><FieldLabel>Status</FieldLabel><Select v-model="statusModel"><SelectTrigger class="w-full"><SelectValue /></SelectTrigger><SelectContent><SelectGroup><SelectItem v-for="item in noteStatuses" :key="item.value" :value="item.value">{{ item.label }}</SelectItem></SelectGroup></SelectContent></Select></Field>
            </div>
          </FieldGroup>

          <FieldSet v-if="!fresh && tags.length"><FieldLegend variant="label">Tags</FieldLegend><FieldGroup class="grid grid-cols-2 gap-2 sm:grid-cols-3"><Field v-for="tag in tags" :key="tag.id" orientation="horizontal"><Checkbox :id="`tag-${tag.id}`" :model-value="tagIds.includes(tag.id)" @update:model-value="toggleTag(tag.id)" /><FieldLabel :for="`tag-${tag.id}`" class="font-normal">{{ tag.name }}</FieldLabel></Field></FieldGroup></FieldSet>

          <div v-if="!fresh && selectedTags.length" class="flex flex-wrap gap-1.5"><Badge v-for="tag in selectedTags" :key="tag.id" variant="secondary">{{ tag.name }}</Badge></div>
          <Field><FieldLabel for="note-content" class="sr-only">Note content</FieldLabel><Textarea id="note-content" v-model="content" class="min-h-[45vh]" spellcheck="true" placeholder="Start writing…" /></Field>
          <div class="flex items-center justify-between text-[10px] text-muted-foreground"><span>{{ content.length }} characters</span><span><template v-if="!fresh">{{ selectedNotebookName }} · </template>{{ wordCount }} words</span></div>
        </div>
      </ScrollArea>
    </template>
    <Empty v-else class="flex-1"><EmptyHeader><EmptyMedia variant="icon"><HugeiconsIcon :icon="Note01Icon" :size="18" /></EmptyMedia><EmptyTitle>Select a note to begin</EmptyTitle><EmptyDescription>Your ideas are ready when you are.</EmptyDescription></EmptyHeader><EmptyContent><Button :disabled="busy" @click="emit('create')"><HugeiconsIcon :icon="NoteAddIcon" data-icon="inline-start" />Create a new note</Button></EmptyContent></Empty>
  </section>
</template>
