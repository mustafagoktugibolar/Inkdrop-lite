<script setup lang="ts">
import { HugeiconsIcon } from '@hugeicons/vue'
import { Copy01Icon, Note01Icon } from '@hugeicons/core-free-icons'

import type { NoteResponse } from '@/api/notes'
import { notebookPath } from '@/lib/notebookTree'
import type { NotebookResponse } from '@/api/notebooks'

import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { ScrollArea } from '@/components/ui/scroll-area'

defineProps<{
  notebooks: NotebookResponse[]
  notes: NoteResponse[]
}>()

const open = defineModel<boolean>('open', { required: true })

const emit = defineEmits<{
  select: [note: NoteResponse]
}>()

function excerpt(content: string) {
  return content.replace(/[#>*_`\-[\]()]/g, ' ').replace(/\s+/g, ' ').trim() || 'Empty note'
}

function pick(note: NoteResponse) {
  emit('select', note)
  open.value = false
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="sm:max-w-lg">
      <DialogHeader>
        <DialogTitle>Start from a template</DialogTitle>
        <DialogDescription>Pick a note to copy. It becomes the starting point for a new note — the original stays untouched.</DialogDescription>
      </DialogHeader>

      <ScrollArea v-if="notes.length" class="max-h-[60dvh]">
        <div class="flex flex-col pr-3">
          <button v-for="note in notes" :key="note.id" type="button" class="flex flex-col gap-1 rounded-lg border-b px-3 py-3 text-left transition-colors last:border-b-0 hover:bg-muted/50" @click="pick(note)">
            <div class="flex items-center gap-2"><HugeiconsIcon :icon="Copy01Icon" :size="12" class="shrink-0 text-muted-foreground" /><strong class="min-w-0 flex-1 truncate text-xs font-medium">{{ note.title || 'Untitled' }}</strong></div>
            <p class="truncate text-[11px] text-muted-foreground">{{ notebookPath(notebooks, note.notebookId) }} · {{ excerpt(note.content) }}</p>
          </button>
        </div>
      </ScrollArea>
      <Empty v-else class="min-h-40"><EmptyHeader><EmptyMedia variant="icon"><HugeiconsIcon :icon="Note01Icon" :size="18" /></EmptyMedia><EmptyTitle>No notes to copy from yet</EmptyTitle><EmptyDescription>Write your first note, then reuse it as a template here.</EmptyDescription></EmptyHeader></Empty>
    </DialogContent>
  </Dialog>
</template>
