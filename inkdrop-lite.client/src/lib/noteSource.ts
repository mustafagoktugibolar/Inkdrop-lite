/** Maps a note's `createdSource`/`updatedSource` ('app' or 'mcp:{client}/{version}') to a label, or null for the web app. */
export function sourceLabel(source: string): string | null {
  if (source === 'app') return null
  const client = /^mcp:([^/]+)/.exec(source)?.[1]
  return client ?? 'agent'
}
