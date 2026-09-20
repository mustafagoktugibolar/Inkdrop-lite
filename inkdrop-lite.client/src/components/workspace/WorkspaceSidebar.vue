<script setup lang="ts">
import { HugeiconsIcon } from '@hugeicons/vue'
import {
  Add01Icon,
  Cancel01Icon,
  CheckmarkCircle02Icon,
  File01Icon,
  Folder01Icon,
  Logout03Icon,
  Moon02Icon,
  Note01Icon,
  NoteAddIcon,
  PauseCircleIcon,
  PlayCircleIcon,
  Sun03Icon,
  Tag01Icon,
} from '@hugeicons/core-free-icons'

import type { NotebookResponse } from '@/api/notebooks'
import { NoteStatus, type NoteResponse } from '@/api/notes'
import type { TagResponse } from '@/api/tags'
import type { MobilePane, NavFilter, Theme } from '@/types/workspace'
import { cn } from '@/lib/utils'

import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'

const props = defineProps<{
  accountName: string
  activeFilter: NavFilter
  busy: boolean
  mobilePane: MobilePane
  notebooks: NotebookResponse[]
  notes: NoteResponse[]
  tags: TagResponse[]
  theme: Theme
  themeLabel: string
}>()

const emit = defineEmits<{
  create: []
  filter: [filter: NavFilter]
  manage: [tab: 'notebooks' | 'tags']
  signOut: []
  toggleTheme: []
}>()

const noteStatuses = [
  { value: NoteStatus.Active, label: 'Active', icon: PlayCircleIcon },
  { value: NoteStatus.OnHold, label: 'On Hold', icon: PauseCircleIcon },
  { value: NoteStatus.Completed, label: 'Completed', icon: CheckmarkCircle02Icon },
  { value: NoteStatus.Dropped, label: 'Dropped', icon: Cancel01Icon },
]

function noteCount(kind: 'notebook' | 'tag' | 'status', id: string | NoteStatus) {
  if (kind === 'notebook') return props.notes.filter((note) => note.notebookId === id).length
  if (kind === 'tag') return props.notes.filter((note) => note.tagIds?.includes(id as string)).length
  return props.notes.filter((note) => note.status === id).length
}

function initials(value: string) {
  const name = value.split('@')[0] ?? value
  return name.split(/[._-]/).map((part) => part[0] ?? '').join('').slice(0, 2).toUpperCase()
}
</script>

<template>
  <aside :class="cn('min-h-0 flex-col border-r bg-sidebar text-sidebar-foreground lg:flex', mobilePane === 'library' ? 'flex' : 'hidden')">
    <div class="hidden h-12 items-center gap-2 border-b px-4 lg:flex">
      <span class="grid size-7 place-items-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground"><HugeiconsIcon :icon="Note01Icon" :size="14" /></span>
      <b class="text-xs">Inkdrop Lite</b>
    </div>
    <ScrollArea class="flex-1">
      <nav class="flex flex-col gap-1 p-2">
        <Button :variant="activeFilter.kind === 'all' ? 'secondary' : 'ghost'" class="w-full justify-start" @click="emit('filter', { kind: 'all', label: 'All Notes' })"><HugeiconsIcon :icon="File01Icon" data-icon="inline-start" /><span class="flex-1 text-left">All Notes</span><Badge variant="outline">{{ notes.length }}</Badge></Button>
        <Button variant="ghost" class="w-full justify-start" :disabled="busy" @click="emit('create')"><HugeiconsIcon :icon="NoteAddIcon" data-icon="inline-start" />New note</Button>

        <div class="mt-4 flex items-center justify-between px-2"><span class="text-[10px] font-medium uppercase tracking-widest text-muted-foreground">Notebooks</span><Button variant="ghost" size="icon-xs" aria-label="Manage notebooks" @click="emit('manage', 'notebooks')"><HugeiconsIcon :icon="Add01Icon" /></Button></div>
        <Button v-for="book in notebooks" :key="book.id" :variant="activeFilter.kind === 'notebook' && activeFilter.id === book.id ? 'secondary' : 'ghost'" class="w-full justify-start" @click="emit('filter', { kind: 'notebook', id: book.id, label: book.name })"><HugeiconsIcon :icon="Folder01Icon" data-icon="inline-start" /><span class="flex-1 truncate text-left">{{ book.name }}</span><span class="text-[10px] text-muted-foreground">{{ noteCount('notebook', book.id) }}</span></Button>
        <p v-if="!notebooks.length" class="px-2 py-2 text-xs text-muted-foreground">No notebooks yet</p>

        <div class="mt-4 px-2"><span class="text-[10px] font-medium uppercase tracking-widest text-muted-foreground">Status</span></div>
        <Button v-for="status in noteStatuses" :key="status.value" :variant="activeFilter.kind === 'status' && activeFilter.id === status.value ? 'secondary' : 'ghost'" class="w-full justify-start" @click="emit('filter', { kind: 'status', id: status.value, label: status.label })"><HugeiconsIcon :icon="status.icon" data-icon="inline-start" /><span class="flex-1 text-left">{{ status.label }}</span><span class="text-[10px] text-muted-foreground">{{ noteCount('status', status.value) }}</span></Button>

        <div class="mt-4 flex items-center justify-between px-2"><span class="text-[10px] font-medium uppercase tracking-widest text-muted-foreground">Tags</span><Button variant="ghost" size="icon-xs" aria-label="Manage tags" @click="emit('manage', 'tags')"><HugeiconsIcon :icon="Add01Icon" /></Button></div>
        <Button v-for="tag in tags" :key="tag.id" :variant="activeFilter.kind === 'tag' && activeFilter.id === tag.id ? 'secondary' : 'ghost'" class="w-full justify-start" @click="emit('filter', { kind: 'tag', id: tag.id, label: tag.name })"><HugeiconsIcon :icon="Tag01Icon" data-icon="inline-start" /><span class="flex-1 truncate text-left">{{ tag.name }}</span><span class="text-[10px] text-muted-foreground">{{ noteCount('tag', tag.id) }}</span></Button>
      </nav>
    </ScrollArea>
    <Separator />
    <div class="flex items-center gap-2 p-3">
      <Avatar size="sm"><AvatarFallback>{{ initials(accountName) }}</AvatarFallback></Avatar>
      <div class="min-w-0 flex-1"><b class="block truncate text-xs">{{ accountName.split('@')[0] }}</b><span class="block truncate text-[10px] text-muted-foreground">{{ accountName }}</span></div>
      <Button variant="ghost" size="icon-sm" :aria-label="themeLabel" :title="themeLabel" @click="emit('toggleTheme')"><HugeiconsIcon :icon="theme === 'dark' ? Sun03Icon : Moon02Icon" /></Button>
      <Button variant="ghost" size="icon-sm" title="Sign out" :disabled="busy" @click="emit('signOut')"><HugeiconsIcon :icon="Logout03Icon" /></Button>
    </div>
  </aside>
</template>
