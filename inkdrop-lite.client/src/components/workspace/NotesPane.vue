<script setup lang="ts">
import { HugeiconsIcon } from '@hugeicons/vue'
import { Add01Icon, ArrowLeft01Icon, Note01Icon, NoteAddIcon, PinIcon, Robot01Icon, Search01Icon } from '@hugeicons/core-free-icons'

import { NoteStatus, type NoteResponse } from '@/api/notes'
import type { TagResponse } from '@/api/tags'
import type { MobilePane, NavFilter } from '@/types/workspace'
import { cn } from '@/lib/utils'
import { sourceLabel } from '@/lib/noteSource'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty'
import { Input } from '@/components/ui/input'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/components/ui/input-group'
import { ScrollArea } from '@/components/ui/scroll-area'

defineProps<{
  activeFilter: NavFilter
  busy: boolean
  mobilePane: MobilePane
  notes: NoteResponse[]
  renamingNoteId: string | null
  selectedNoteId: string | null
  tags: TagResponse[]
}>()

const query = defineModel<string>('query', { required: true })
const renameTitle = defineModel<string>('renameTitle', { required: true })

const emit = defineEmits<{
  cancelRename: []
  commitRename: [note: NoteResponse]
  create: []
  openLibrary: []
  select: [note: NoteResponse]
  startRename: [note: NoteResponse]
}>()

function excerpt(content: string) {
  return content.replace(/[#>*_`\-[\]()]/g, ' ').replace(/\s+/g, ' ').trim() || 'Empty note'
}

function parseUtc(value: string) {
  return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`)
}

function relativeDate(value: string) {
  const date = parseUtc(value)
  const minutes = Math.max(1, Math.floor((Date.now() - date.getTime()) / 60000))
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  if (days < 7) return `${days}d ago`
  return new Intl.DateTimeFormat('en', { month: 'short', day: 'numeric' }).format(date)
}
</script>

<template>
  <section :class="cn('min-h-0 flex-col border-r bg-card lg:flex', mobilePane === 'notes' ? 'flex' : 'hidden')">
    <header class="flex h-12 items-center justify-between border-b px-4"><Button class="lg:hidden" variant="ghost" size="icon-sm" @click="emit('openLibrary')"><HugeiconsIcon :icon="ArrowLeft01Icon" /></Button><div class="min-w-0 flex-1"><h2 class="truncate text-sm font-medium">{{ activeFilter.label }}</h2><p class="text-[10px] text-muted-foreground">{{ notes.length }} {{ notes.length === 1 ? 'note' : 'notes' }}</p></div><Button size="icon-sm" aria-label="Create note" :disabled="busy" @click="emit('create')"><HugeiconsIcon :icon="Add01Icon" /></Button></header>
    <div class="p-3"><InputGroup><InputGroupAddon><HugeiconsIcon :icon="Search01Icon" :size="14" /></InputGroupAddon><InputGroupInput v-model="query" type="search" placeholder="Search notes…" /></InputGroup></div>
    <ScrollArea class="flex-1">
      <div v-if="notes.length" class="flex flex-col">
        <div v-for="note in notes" :key="note.id" role="button" tabindex="0" :class="cn('flex flex-col gap-2 border-b p-4 text-left transition-colors hover:bg-muted/50', selectedNoteId === note.id && 'bg-accent')" @click="emit('select', note)" @dblclick.stop="emit('startRename', note)" @keydown.enter="emit('select', note)">
          <div class="flex items-start gap-2"><Input v-if="renamingNoteId === note.id" v-model="renameTitle" autofocus aria-label="Rename note" class="h-7 flex-1" maxlength="200" @click.stop @dblclick.stop @keydown.enter.stop.prevent="emit('commitRename', note)" @keydown.escape.stop.prevent="emit('cancelRename')" @blur="emit('commitRename', note)" /><strong v-else class="flex-1 text-xs font-medium leading-5">{{ note.title || 'Untitled' }}</strong><HugeiconsIcon v-if="note.pinned" :icon="PinIcon" :size="12" class="mt-1 shrink-0 text-muted-foreground" aria-label="Pinned" /><Badge v-if="note.status === NoteStatus.Active" variant="secondary">Active</Badge></div>
          <div class="flex flex-wrap items-center gap-1.5"><time class="text-[10px] text-muted-foreground">{{ relativeDate(note.updatedAt) }}</time><Badge v-if="sourceLabel(note.updatedSource)" variant="outline"><HugeiconsIcon :icon="Robot01Icon" :size="10" data-icon="inline-start" />{{ sourceLabel(note.updatedSource) }}</Badge><Badge v-for="tag in tags.filter((item) => note.tagIds?.includes(item.id)).slice(0, 2)" :key="tag.id" variant="outline">{{ tag.name }}</Badge></div>
          <p class="truncate text-[11px] text-muted-foreground">{{ excerpt(note.content) }}</p>
        </div>
      </div>
      <Empty v-else class="min-h-80"><EmptyHeader><EmptyMedia variant="icon"><HugeiconsIcon :icon="Note01Icon" :size="18" /></EmptyMedia><EmptyTitle>No notes here</EmptyTitle><EmptyDescription>Create a note and start writing.</EmptyDescription></EmptyHeader><EmptyContent><Button size="sm" :disabled="busy" @click="emit('create')"><HugeiconsIcon :icon="NoteAddIcon" data-icon="inline-start" />Create note</Button></EmptyContent></Empty>
    </ScrollArea>
  </section>
</template>
