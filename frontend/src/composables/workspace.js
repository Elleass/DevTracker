import { ref } from 'vue'
import { api } from '@/api/client'
export const user = ref(null)
export const projects = ref([])
export const activeSession = ref(null)
let authCheck
export async function checkSession() {
  if (!authCheck) authCheck = api('/auth/me').then(value => { user.value = value; return value }).catch(() => { user.value = null; return null }).finally(() => { authCheck = null })
  return authCheck
}
export async function refreshProjects() { projects.value = (await api('/projects')).projects }
export async function refreshSession() { activeSession.value = await api('/work-sessions/active') }
export async function logout() { await api('/auth/logout', { method: 'POST' }); user.value = null; projects.value = []; activeSession.value = null }
export function formatDuration(seconds) {
  const total = Math.max(0, Math.floor(seconds || 0))
  return `${Math.floor(total / 3600)}:${String(Math.floor(total / 60) % 60).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`
}
