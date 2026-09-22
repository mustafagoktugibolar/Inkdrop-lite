import MarkdownIt from 'markdown-it'

// html: false keeps raw HTML in notes as text, and markdown-it refuses javascript:/vbscript: links,
// so the rendered output is safe to bind with v-html without a separate sanitizer.
const md = new MarkdownIt({ html: false, linkify: true })

const renderLinkOpen =
  md.renderer.rules.link_open ?? ((tokens, index, options, _env, self) => self.renderToken(tokens, index, options))

md.renderer.rules.link_open = (tokens, index, options, env, self) => {
  tokens[index]!.attrSet('target', '_blank')
  tokens[index]!.attrSet('rel', 'noopener noreferrer')
  return renderLinkOpen(tokens, index, options, env, self)
}

// GitHub-style task lists ("- [ ] text" / "- [x] text"). markdown-it has no built-in
// support for these; this turns the leading "[ ]"/"[x]" of a list item into a disabled
// checkbox. The injected `html_inline` content is a fixed string (only `checked` varies),
// so it stays safe to render even with `html: false` disabling *parsed* raw HTML.
md.core.ruler.after('inline', 'task-lists', (state) => {
  const tokens = state.tokens
  for (let i = 0; i < tokens.length; i++) {
    if (tokens[i]!.type !== 'list_item_open') continue
    const inline = tokens[i + 2]
    const marker = inline?.type === 'inline' ? inline.children?.[0] : undefined
    const match = marker?.type === 'text' ? /^\[([ xX])\]\s+/.exec(marker.content) : null
    if (!match || !inline?.children) continue
    marker!.content = marker!.content.slice(match[0].length)
    const checked = match[1]!.toLowerCase() === 'x'
    const checkbox = new state.Token('html_inline', '', 0)
    checkbox.content = `<input type="checkbox" disabled ${checked ? 'checked' : ''} />`
    inline.children.unshift(checkbox)
    tokens[i]!.attrJoin('class', 'task-list-item')
  }
})

export function renderMarkdown(source: string) {
  return md.render(source)
}
