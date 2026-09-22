<script setup lang="ts">
import { HugeiconsIcon } from '@hugeicons/vue'
import { computed } from 'vue'
import { Delete02Icon, Edit02Icon, Tag01Icon } from '@hugeicons/core-free-icons'

import type { NotebookIconType, NotebookResponse } from '@/api/notebooks'
import type { NoteResponse } from '@/api/notes'
import type { TagResponse } from '@/api/tags'

import { buildNotebookRows, notebookAndDescendantIds, notebookPath, ROOT_NOTEBOOK } from '@/lib/notebookTree'

import NotebookIcon from '@/components/workspace/NotebookIcon.vue'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Field, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Separator } from '@/components/ui/separator'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Textarea } from '@/components/ui/textarea'

const props = defineProps<{
  busy: boolean
  editingNotebookId: string | null
  editingTagId: string | null
  notebooks: NotebookResponse[]
  notes: NoteResponse[]
  tags: TagResponse[]
}>()

const open = defineModel<boolean>('open', { required: true })
const tab = defineModel<'notebooks' | 'tags'>('tab', { required: true })
const notebookName = defineModel<string>('notebookName', { required: true })
const notebookParent = defineModel<string>('notebookParent', { required: true })
const notebookOrder = defineModel<string>('notebookOrder', { required: true })
const notebookIconType = defineModel<NotebookIconType>('notebookIconType', { required: true })
const notebookIconSvg = defineModel<string>('notebookIconSvg', { required: true })
const notebookIconAttachmentId = defineModel<string | null>('notebookIconAttachmentId', { required: true })
const tagName = defineModel<string>('tagName', { required: true })

const emit = defineEmits<{
  deleteNotebook: [notebook: NotebookResponse]
  deleteTag: [tag: TagResponse]
  editNotebook: [notebook: NotebookResponse]
  editTag: [tag: TagResponse]
  resetNotebook: []
  resetTag: []
  saveNotebook: []
  saveTag: []
  uploadIcon: [file: File]
}>()

const notebookRows = computed(() => buildNotebookRows(props.notebooks))
// A notebook cannot be moved under itself or its descendants (the API rejects it too).
const parentChoices = computed(() => {
  const blocked = new Set(props.editingNotebookId ? notebookAndDescendantIds(props.notebooks, props.editingNotebookId) : [])
  return buildNotebookRows(props.notebooks).filter((row) => !blocked.has(row.book.id))
})
const iconTypeModel = computed({
  get: () => notebookIconType.value as string,
  set: (value: string) => { notebookIconType.value = value as NotebookIconType },
})

function onIconChosen(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (file) emit('uploadIcon', file)
}

function updateNotebookOrder(value: string | number) {
  notebookOrder.value = String(value)
}

function notebookNoteCount(id: string) {
  return props.notes.filter((note) => note.notebookId === id).length
}

function tagNoteCount(id: string) {
  return props.notes.filter((note) => note.tagIds.includes(id)).length
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="sm:max-w-2xl">
      <DialogHeader><DialogTitle>Organize your workspace</DialogTitle><DialogDescription>Create, rename, or remove notebooks and tags.</DialogDescription></DialogHeader>
      <Tabs v-model="tab">
        <TabsList><TabsTrigger value="notebooks">Notebooks</TabsTrigger><TabsTrigger value="tags">Tags</TabsTrigger></TabsList>
        <TabsContent value="notebooks" class="flex flex-col gap-4">
          <form @submit.prevent="emit('saveNotebook')">
            <FieldGroup>
              <Field><FieldLabel for="book-name">Name</FieldLabel><Input id="book-name" v-model.trim="notebookName" required maxlength="64" placeholder="Notebook name" /></Field>
              <div class="grid gap-3 sm:grid-cols-[1fr_6rem_1fr]">
                <Field><FieldLabel>Parent notebook</FieldLabel><Select v-model="notebookParent"><SelectTrigger class="w-full"><SelectValue /></SelectTrigger><SelectContent><SelectGroup><SelectItem :value="ROOT_NOTEBOOK">None (top level)</SelectItem><SelectItem v-for="row in parentChoices" :key="row.book.id" :value="row.book.id">{{ notebookPath(notebooks, row.book.id) }}</SelectItem></SelectGroup></SelectContent></Select></Field>
                <Field><FieldLabel for="book-order">Order</FieldLabel><Input id="book-order" :model-value="notebookOrder" type="number" step="1" placeholder="Auto" @update:model-value="updateNotebookOrder" /></Field>
                <Field><FieldLabel>Icon</FieldLabel><Select v-model="iconTypeModel"><SelectTrigger class="w-full"><SelectValue /></SelectTrigger><SelectContent><SelectGroup><SelectItem value="None">Default folder</SelectItem><SelectItem value="Svg">Custom SVG</SelectItem><SelectItem value="Attachment">Uploaded image</SelectItem></SelectGroup></SelectContent></Select></Field>
              </div>
              <Field v-if="notebookIconType === 'Svg'"><FieldLabel for="book-svg">SVG markup</FieldLabel><Textarea id="book-svg" v-model="notebookIconSvg" class="min-h-20 font-mono text-xs" maxlength="262144" placeholder="<svg xmlns=&quot;http://www.w3.org/2000/svg&quot; viewBox=&quot;0 0 24 24&quot;>…</svg>" /></Field>
              <Field v-if="notebookIconType === 'Attachment'"><FieldLabel for="book-icon-file">Icon image</FieldLabel><div class="flex items-center gap-3"><NotebookIcon v-if="notebookIconAttachmentId" :notebook="{ iconType: 'Attachment', iconSvg: null, iconAttachmentId: notebookIconAttachmentId }" :size="24" /><Input id="book-icon-file" type="file" accept="image/*" :disabled="busy" @change="onIconChosen" /></div></Field>
              <div class="flex gap-2"><Button type="submit" :disabled="busy">{{ editingNotebookId ? 'Update notebook' : 'Add notebook' }}</Button><Button v-if="editingNotebookId" type="button" variant="outline" @click="emit('resetNotebook')">Cancel</Button></div>
            </FieldGroup>
          </form>
          <Separator />
          <ScrollArea class="max-h-64"><div class="flex flex-col gap-2"><div v-for="row in notebookRows" :key="row.book.id" class="flex items-center gap-3 rounded-lg border p-3" :style="{ marginLeft: `${row.depth * 1}rem` }"><NotebookIcon :notebook="row.book" /><div class="min-w-0 flex-1"><b class="block truncate text-xs">{{ row.book.name }}</b><span class="block truncate text-[10px] text-muted-foreground">{{ `${notebookNoteCount(row.book.id)} notes` }}</span></div><Button variant="ghost" size="icon-sm" aria-label="Edit notebook" @click="emit('editNotebook', row.book)"><HugeiconsIcon :icon="Edit02Icon" /></Button><Button variant="destructive" size="icon-sm" aria-label="Delete notebook" @click="emit('deleteNotebook', row.book)"><HugeiconsIcon :icon="Delete02Icon" /></Button></div></div></ScrollArea>
        </TabsContent>
        <TabsContent value="tags" class="flex flex-col gap-4">
          <form @submit.prevent="emit('saveTag')"><FieldGroup><Field><FieldLabel for="tag-name">Name</FieldLabel><Input id="tag-name" v-model.trim="tagName" required maxlength="100" placeholder="Tag name" /></Field><div class="flex gap-2"><Button type="submit" :disabled="busy">{{ editingTagId ? 'Update tag' : 'Add tag' }}</Button><Button v-if="editingTagId" type="button" variant="outline" @click="emit('resetTag')">Cancel</Button></div></FieldGroup></form>
          <Separator />
          <ScrollArea class="max-h-64"><div class="flex flex-col gap-2"><div v-for="tagItem in tags" :key="tagItem.id" class="flex items-center gap-3 rounded-lg border p-3"><HugeiconsIcon :icon="Tag01Icon" :size="16" /><div class="min-w-0 flex-1"><b class="block truncate text-xs">{{ tagItem.name }}</b><span class="text-[10px] text-muted-foreground">{{ tagNoteCount(tagItem.id) }} notes</span></div><Button variant="ghost" size="icon-sm" @click="emit('editTag', tagItem)"><HugeiconsIcon :icon="Edit02Icon" /></Button><Button variant="destructive" size="icon-sm" @click="emit('deleteTag', tagItem)"><HugeiconsIcon :icon="Delete02Icon" /></Button></div></div></ScrollArea>
        </TabsContent>
      </Tabs>
    </DialogContent>
  </Dialog>
</template>
