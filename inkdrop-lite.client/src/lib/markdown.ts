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

export function renderMarkdown(source: string) {
  return md.render(source)
}
