export class ApiError extends Error {
  constructor(status, body, retryAfter) {
    super(Array.isArray(body) ? body.map(e => e.description).join(' ') : body?.detail || body?.title || `Request failed (${status}).`)
    this.status = status
    this.fields = body?.errors || {}
    this.traceId = body?.traceId
    this.retryAfter = Number(retryAfter) || 0
  }
}
export async function api(path, { body, ...options } = {}) {
  const response = await fetch(`/api${path}`, {
    credentials: 'same-origin', ...options,
    headers: { ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}), ...options.headers },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (response.status === 204) return null
  const data = await response.json().catch(() => null)
  if (!response.ok) {
    if (response.status === 401 && path !== '/auth/login') window.dispatchEvent(new Event('session-expired'))
    throw new ApiError(response.status, data, response.headers.get('Retry-After'))
  }
  return data
}
