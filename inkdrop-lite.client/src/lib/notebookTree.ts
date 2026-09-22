import type { NotebookResponse } from '@/api/notebooks'

/** Select value for "no parent"; reka-ui selects cannot use an empty string. */
export const ROOT_NOTEBOOK = '__root__'

export interface NotebookRow {
  book: NotebookResponse
  depth: number
  hasChildren: boolean
}

function childrenByParent(notebooks: NotebookResponse[]) {
  const ids = new Set(notebooks.map((book) => book.id))
  const map = new Map<string | null, NotebookResponse[]>()
  for (const book of notebooks) {
    // A parent that is not in the list (e.g. removed meanwhile) is treated as a root.
    const key = book.parentNotebookId && ids.has(book.parentNotebookId) ? book.parentNotebookId : null
    map.set(key, [...(map.get(key) ?? []), book])
  }
  return map
}

/** Depth-first rows in display order, skipping the children of collapsed notebooks. */
export function buildNotebookRows(notebooks: NotebookResponse[], collapsed: ReadonlySet<string> = new Set()) {
  const map = childrenByParent(notebooks)
  const rows: NotebookRow[] = []
  const visit = (parent: string | null, depth: number) => {
    for (const book of map.get(parent) ?? []) {
      const hasChildren = (map.get(book.id)?.length ?? 0) > 0
      rows.push({ book, depth, hasChildren })
      if (hasChildren && !collapsed.has(book.id)) visit(book.id, depth + 1)
    }
  }
  visit(null, 0)
  return rows
}

/** The notebook's id followed by the ids of all its descendants. */
export function notebookAndDescendantIds(notebooks: NotebookResponse[], id: string) {
  const map = childrenByParent(notebooks)
  const result = [id]
  for (let index = 0; index < result.length; index++) {
    for (const child of map.get(result[index]!) ?? []) result.push(child.id)
  }
  return result
}

/** "Parent / Child" label for pickers. */
export function notebookPath(notebooks: NotebookResponse[], id: string) {
  const byId = new Map(notebooks.map((book) => [book.id, book]))
  const names: string[] = []
  const seen = new Set<string>()
  for (let current = byId.get(id); current && !seen.has(current.id); current = current.parentNotebookId ? byId.get(current.parentNotebookId) : undefined) {
    seen.add(current.id)
    names.unshift(current.name)
  }
  return names.join(' / ')
}

/** Data URI so user-supplied SVG is rendered as an image and can never run script. */
export function svgDataUri(svg: string) {
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`
}
