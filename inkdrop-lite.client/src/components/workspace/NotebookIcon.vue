<script setup lang="ts">
import { ref, watch } from 'vue'
import { HugeiconsIcon } from '@hugeicons/vue'
import { Folder01Icon } from '@hugeicons/core-free-icons'

import { attachmentsApi } from '@/api/attachments'
import type { NotebookResponse } from '@/api/notebooks'
import { svgDataUri } from '@/lib/notebookTree'

const props = defineProps<{
  notebook: Pick<NotebookResponse, 'iconType' | 'iconSvg' | 'iconAttachmentId'>
  size?: number
}>()

// Attachment bytes need the bearer token, so fetch them and show an object URL. Cached per id.
const urlCache = new Map<string, Promise<string>>()
const attachmentUrl = ref<string | null>(null)

watch(
  () => props.notebook.iconType === 'Attachment' ? props.notebook.iconAttachmentId : null,
  async (id) => {
    attachmentUrl.value = null
    if (!id) return
    if (!urlCache.has(id)) urlCache.set(id, attachmentsApi.content(id).then((blob) => URL.createObjectURL(blob)))
    try {
      const url = await urlCache.get(id)!
      if (props.notebook.iconAttachmentId === id) attachmentUrl.value = url
    } catch {
      urlCache.delete(id)
    }
  },
  { immediate: true },
)
</script>

<template>
  <img v-if="notebook.iconType === 'Svg' && notebook.iconSvg" :src="svgDataUri(notebook.iconSvg)" alt="" :width="size ?? 16" :height="size ?? 16" class="shrink-0" />
  <img v-else-if="attachmentUrl" :src="attachmentUrl" alt="" :width="size ?? 16" :height="size ?? 16" class="shrink-0 object-contain" />
  <HugeiconsIcon v-else :icon="Folder01Icon" :size="size ?? 16" data-icon="inline-start" />
</template>
