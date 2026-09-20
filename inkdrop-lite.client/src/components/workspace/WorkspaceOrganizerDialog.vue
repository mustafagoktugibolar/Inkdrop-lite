<script setup lang="ts">
import { HugeiconsIcon } from '@hugeicons/vue'
import { Delete02Icon, Edit02Icon, Folder01Icon, Tag01Icon } from '@hugeicons/core-free-icons'

import type { NotebookResponse } from '@/api/notebooks'
import type { NoteResponse } from '@/api/notes'
import type { TagResponse } from '@/api/tags'

import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Field, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'

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
}>()

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
          <form @submit.prevent="emit('saveNotebook')"><FieldGroup><Field><FieldLabel for="book-name">Name</FieldLabel><Input id="book-name" v-model.trim="notebookName" required maxlength="100" placeholder="Notebook name" /></Field><div class="flex gap-2"><Button type="submit" :disabled="busy">{{ editingNotebookId ? 'Update notebook' : 'Add notebook' }}</Button><Button v-if="editingNotebookId" type="button" variant="outline" @click="emit('resetNotebook')">Cancel</Button></div></FieldGroup></form>
          <Separator />
          <ScrollArea class="max-h-64"><div class="flex flex-col gap-2"><div v-for="book in notebooks" :key="book.id" class="flex items-center gap-3 rounded-lg border p-3"><HugeiconsIcon :icon="Folder01Icon" :size="16" /><div class="min-w-0 flex-1"><b class="block truncate text-xs">{{ book.name }}</b><span class="block truncate text-[10px] text-muted-foreground">{{ `${notebookNoteCount(book.id)} notes` }}</span></div><Button variant="ghost" size="icon-sm" @click="emit('editNotebook', book)"><HugeiconsIcon :icon="Edit02Icon" /></Button><Button variant="destructive" size="icon-sm" @click="emit('deleteNotebook', book)"><HugeiconsIcon :icon="Delete02Icon" /></Button></div></div></ScrollArea>
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
