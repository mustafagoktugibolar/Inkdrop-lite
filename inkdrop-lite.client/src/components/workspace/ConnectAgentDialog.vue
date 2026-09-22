<script setup lang="ts">
import { computed, ref } from 'vue'
import { HugeiconsIcon } from '@hugeicons/vue'
import { Copy01Icon, Tick02Icon } from '@hugeicons/core-free-icons'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'

const open = defineModel<boolean>('open', { required: true })

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL || window.location.origin).replace(/\/$/, '')
const mcpUrl = (import.meta.env.VITE_MCP_URL || `${apiBaseUrl}/mcp`).replace(/\/$/, '')

const snippets = computed(() => ({
  claudeCode: `claude mcp add --transport http inkdrop ${mcpUrl}`,
  cursor: JSON.stringify({ mcpServers: { inkdrop: { url: mcpUrl } } }, null, 2),
  vscode: JSON.stringify({ servers: { inkdrop: { type: 'http', url: mcpUrl } } }, null, 2),
}))

const tools = [
  { name: 'list_notebooks', text: 'List notebooks and their hierarchy' },
  { name: 'list_tags', text: 'List tags' },
  { name: 'search_notes', text: 'Search by text, notebook, tag or status' },
  { name: 'get_note', text: 'Read a note, or just one section of a long note' },
  { name: 'create_note', text: 'Create a Markdown note in a notebook' },
  { name: 'update_note', text: 'Edit title, content, status, pin, notebook or tags' },
]

const copied = ref<string | null>(null)

async function copy(key: string, value: string) {
  try {
    await navigator.clipboard.writeText(value)
    copied.value = key
    window.setTimeout(() => { if (copied.value === key) copied.value = null }, 1500)
  } catch {
    copied.value = null
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="sm:max-w-2xl">
      <DialogHeader>
        <DialogTitle>Connect to an AI agent</DialogTitle>
        <DialogDescription>Add Inkdrop Lite as an MCP server so agents can read and write your notes as you.</DialogDescription>
      </DialogHeader>

      <ScrollArea class="max-h-[70dvh]">
        <div class="flex flex-col gap-5 pr-3">
          <section class="flex flex-col gap-2">
            <h3 class="text-xs font-medium">Server URL</h3>
            <div class="flex items-center gap-2 rounded-lg border bg-muted/40 p-2">
              <code class="min-w-0 flex-1 truncate px-1 text-xs">{{ mcpUrl }}</code>
              <Button variant="ghost" size="icon-sm" aria-label="Copy server URL" @click="copy('url', mcpUrl)"><HugeiconsIcon :icon="copied === 'url' ? Tick02Icon : Copy01Icon" /></Button>
            </div>
          </section>

          <section class="flex flex-col gap-2">
            <h3 class="text-xs font-medium">Add it to your client</h3>
            <Tabs default-value="claude-code">
              <TabsList>
                <TabsTrigger value="claude-code">Claude Code</TabsTrigger>
                <TabsTrigger value="claude-desktop">Claude Desktop</TabsTrigger>
                <TabsTrigger value="cursor">Cursor</TabsTrigger>
                <TabsTrigger value="vscode">VS Code</TabsTrigger>
              </TabsList>

              <TabsContent value="claude-code" class="flex flex-col gap-2">
                <p class="text-xs text-muted-foreground">Run this in your terminal:</p>
                <div class="flex items-start gap-2 rounded-lg border bg-muted/40 p-2">
                  <pre class="min-w-0 flex-1 overflow-x-auto px-1 text-xs">{{ snippets.claudeCode }}</pre>
                  <Button variant="ghost" size="icon-sm" aria-label="Copy command" @click="copy('cc', snippets.claudeCode)"><HugeiconsIcon :icon="copied === 'cc' ? Tick02Icon : Copy01Icon" /></Button>
                </div>
                <p class="text-xs text-muted-foreground">Then run <code>/mcp</code> inside Claude Code and choose <b>inkdrop</b> to sign in.</p>
              </TabsContent>

              <TabsContent value="claude-desktop" class="flex flex-col gap-2">
                <ol class="flex list-decimal flex-col gap-1 pl-4 text-xs text-muted-foreground">
                  <li>Open <b>Settings → Connectors</b> and choose <b>Add custom connector</b>.</li>
                  <li>Name it <code>Inkdrop</code> and paste the server URL above.</li>
                  <li>Click <b>Connect</b> and sign in when your browser opens.</li>
                </ol>
              </TabsContent>

              <TabsContent value="cursor" class="flex flex-col gap-2">
                <p class="text-xs text-muted-foreground">Add this to <code>~/.cursor/mcp.json</code>:</p>
                <div class="flex items-start gap-2 rounded-lg border bg-muted/40 p-2">
                  <pre class="min-w-0 flex-1 overflow-x-auto px-1 text-xs">{{ snippets.cursor }}</pre>
                  <Button variant="ghost" size="icon-sm" aria-label="Copy Cursor config" @click="copy('cursor', snippets.cursor)"><HugeiconsIcon :icon="copied === 'cursor' ? Tick02Icon : Copy01Icon" /></Button>
                </div>
              </TabsContent>

              <TabsContent value="vscode" class="flex flex-col gap-2">
                <p class="text-xs text-muted-foreground">Add this to <code>.vscode/mcp.json</code>:</p>
                <div class="flex items-start gap-2 rounded-lg border bg-muted/40 p-2">
                  <pre class="min-w-0 flex-1 overflow-x-auto px-1 text-xs">{{ snippets.vscode }}</pre>
                  <Button variant="ghost" size="icon-sm" aria-label="Copy VS Code config" @click="copy('vscode', snippets.vscode)"><HugeiconsIcon :icon="copied === 'vscode' ? Tick02Icon : Copy01Icon" /></Button>
                </div>
              </TabsContent>
            </Tabs>
          </section>

          <section class="flex flex-col gap-2">
            <h3 class="text-xs font-medium">Signing in</h3>
            <p class="text-xs text-muted-foreground">The first time you connect, your browser opens a Microsoft sign-in. No client id or key is needed. The agent then acts as you, and only sees your own notes.</p>
          </section>

          <section class="flex flex-col gap-2">
            <h3 class="text-xs font-medium">What an agent can do</h3>
            <ul class="flex flex-col gap-1">
              <li v-for="tool in tools" :key="tool.name" class="flex items-baseline gap-2 text-xs"><code class="shrink-0 rounded bg-muted px-1.5 py-0.5">{{ tool.name }}</code><span class="text-muted-foreground">{{ tool.text }}</span></li>
            </ul>
            <p class="text-xs text-muted-foreground">Agents cannot delete anything. Notes they change are labelled with the agent’s name.</p>
          </section>

          <Alert>
            <AlertTitle>Running locally?</AlertTitle>
            <AlertDescription>The dev server only proxies <code>/api</code>. Point your agent at the API directly (for example <code>http://localhost:5208/mcp</code>), or set <code>VITE_MCP_URL</code>.</AlertDescription>
          </Alert>
        </div>
      </ScrollArea>
    </DialogContent>
  </Dialog>
</template>
