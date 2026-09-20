<script setup lang="ts">
import { HugeiconsIcon } from '@hugeicons/vue'
import { BookOpen01Icon, Moon02Icon, Note01Icon, Sun03Icon } from '@hugeicons/core-free-icons'

import type { Theme } from '@/types/workspace'
import { cn } from '@/lib/utils'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'

defineProps<{
  apiOnline: boolean | null
  busy: boolean
  theme: Theme
  themeLabel: string
}>()

const emit = defineEmits<{
  connect: []
  toggleTheme: []
}>()
</script>

<template>
  <main class="min-h-dvh overflow-auto bg-background text-foreground">
    <header class="mx-auto flex h-20 max-w-7xl items-center justify-between px-6">
      <a class="flex items-center gap-2 text-sm font-semibold" href="#">
        <span class="grid size-8 place-items-center rounded-xl bg-primary text-primary-foreground"><HugeiconsIcon :icon="Note01Icon" :size="16" /></span>
        <span>Inkdrop Lite</span>
      </a>
      <div class="flex items-center gap-2">
        <Button variant="ghost" size="icon-sm" :aria-label="themeLabel" :title="themeLabel" @click="emit('toggleTheme')"><HugeiconsIcon :icon="theme === 'dark' ? Sun03Icon : Moon02Icon" /></Button>
        <Badge variant="outline"><span :class="cn('size-1.5 rounded-full', apiOnline ? 'bg-primary' : 'bg-muted-foreground')"></span>{{ apiOnline ? 'API online' : 'Checking API' }}</Badge>
      </div>
    </header>

    <section class="mx-auto grid min-h-[calc(100dvh-5rem)] max-w-7xl items-center gap-14 px-6 py-12 lg:grid-cols-[0.9fr_1.1fr]">
      <div class="mx-auto flex max-w-xl flex-col items-start gap-6 lg:mx-0">
        <Badge variant="secondary">Your private writing space</Badge>
        <div class="flex flex-col gap-4">
          <h1 class="text-5xl font-semibold tracking-[-0.05em] text-balance sm:text-6xl">Your context, beautifully organized.</h1>
          <p class="max-w-lg text-base leading-7 text-muted-foreground">Keep notes, projects, and the small decisions that move your work forward in one calm Markdown workspace.</p>
        </div>
        <Button size="lg" :disabled="busy" @click="emit('connect')"><HugeiconsIcon :icon="BookOpen01Icon" data-icon="inline-start" />{{ busy ? 'Connecting…' : 'Open your workspace' }}</Button>
        <p class="text-xs text-muted-foreground">Secure sign in with your Microsoft account</p>
      </div>

      <Card class="hidden overflow-hidden shadow-2xl md:block">
        <CardHeader class="border-b"><CardTitle>Inkdrop Lite</CardTitle><CardDescription>A focused space for everything worth remembering.</CardDescription></CardHeader>
        <CardContent class="grid min-h-[26rem] grid-cols-[9rem_13rem_1fr] p-0">
          <div class="flex flex-col gap-2 border-r bg-muted/40 p-4 text-xs"><b class="mb-3">All Notes <span class="float-right text-muted-foreground">24</span></b><span class="text-muted-foreground">NOTEBOOKS</span><span>Inbox</span><span>Work</span><span>Ideas</span><Separator class="my-3" /><span class="text-muted-foreground">STATUS</span><span>Active</span><span>Completed</span></div>
          <div class="flex flex-col gap-3 border-r p-4"><b class="text-xs">All Notes</b><div class="h-7 rounded-md bg-muted"></div><div class="flex flex-col gap-1 rounded-md bg-accent p-3"><b class="text-xs">Building a calmer workflow</b><span class="text-[10px] text-muted-foreground">Today · work</span><p class="line-clamp-2 text-[10px] text-muted-foreground">Small systems compound over time...</p></div><div class="flex flex-col gap-1 p-3"><b class="text-xs">Project notes</b><span class="text-[10px] text-muted-foreground">Yesterday · ideas</span></div></div>
          <div class="flex flex-col gap-6 p-8"><Badge variant="outline">WORK · ACTIVE</Badge><h2 class="text-2xl font-semibold">Building a calmer workflow</h2><div class="flex flex-col gap-3 text-sm"><b>## The idea</b><p class="leading-6 text-muted-foreground">Capture context while it is fresh, keep it easy to retrieve, and let the system stay out of the way.</p></div></div>
        </CardContent>
        <CardFooter class="border-t text-xs text-muted-foreground">Markdown notes, notebooks, statuses and tags in one place.</CardFooter>
      </Card>
    </section>
  </main>
</template>
