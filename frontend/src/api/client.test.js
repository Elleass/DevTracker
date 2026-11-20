import { describe, it, expect, vi, afterEach } from 'vitest'
import { api, ApiError } from './client'
afterEach(() => vi.restoreAllMocks())
describe('API contract', () => {
  it('handles empty 204 and sends same-origin cookies', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }))
    expect(await api('/auth/logout', { method: 'POST' })).toBeNull()
    expect(fetch.mock.calls[0][1].credentials).toBe('same-origin')
  })
  it('preserves field errors and trace ID', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ title: 'Validation failed', errors: { Name: ['Required'] }, traceId: 'abc' }), { status: 400 }))
    await expect(api('/projects')).rejects.toMatchObject({ fields: { Name: ['Required'] }, traceId: 'abc', status: 400 })
  })
  it('redirects expired protected requests, not rejected credentials', async () => {
    const listener = vi.fn(); window.addEventListener('session-expired', listener)
    vi.spyOn(globalThis, 'fetch').mockImplementation(() => Promise.resolve(new Response(null, { status: 401 })))
    await expect(api('/auth/login')).rejects.toBeInstanceOf(ApiError)
    expect(listener).not.toHaveBeenCalled()
    await expect(api('/projects')).rejects.toBeInstanceOf(ApiError)
    expect(listener).toHaveBeenCalledOnce(); window.removeEventListener('session-expired', listener)
  })
  it('normalizes Identity errors and retry-after', async () => {
    expect(new ApiError(400, [{ description: 'Weak password' }]).message).toBe('Weak password')
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ title: 'Too many attempts' }), { status: 429, headers: { 'Retry-After': '45' } }))
    await expect(api('/auth/login')).rejects.toMatchObject({ retryAfter: 45, status: 429 })
  })
})
