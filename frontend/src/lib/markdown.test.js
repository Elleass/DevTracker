import { it, expect } from 'vitest'
import { renderMarkdown } from './markdown'
it('keeps formatted Markdown and removes executable HTML and links', () => {
  const html = renderMarkdown('# Notes\n**saved**\n<script>alert(1)</script><img src=x onerror=alert(1)>\n[bad](javascript:alert(1))')
  expect(html).toContain('<strong>saved</strong>')
  expect(html).not.toMatch(/<script|onerror|href="javascript:/i)
})
