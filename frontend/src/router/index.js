import { createRouter, createWebHistory } from 'vue-router'
import { checkSession, user, activeSession } from '@/composables/workspace'
const router = createRouter({ history: createWebHistory(), routes: [
  { path: '/login', name: 'login', component: () => import('@/views/LoginView.vue') },
  { path: '/', redirect: '/projects' },
  { path: '/', component: () => import('@/layouts/AppLayout.vue'), meta: { requiresAuth: true }, children: [
    { path: 'projects', name: 'projects', component: () => import('@/views/ProjectView.vue') },
    { path: 'projects/:projectId', name: 'project-overview', component: () => import('@/views/ProjectOverviewView.vue') },
    { path: 'sessions', name: 'sessions', component: () => import('@/views/SessionsView.vue') },
    { path: 'admin', name: 'admin', component: () => import('@/views/AdminView.vue'), meta: { admin: true } },
  ] },
  { path: '/:pathMatch(.*)*', component: () => import('@/views/NotFoundView.vue') },
] })
router.beforeEach(async to => {
  if (!to.meta.requiresAuth) return true
  const account = await checkSession()
  if (!account) return { name: 'login', query: { redirect: to.fullPath } }
  if (to.meta.admin && !account.roles.includes('ADMIN')) return { name: 'projects' }
})
window.addEventListener('session-expired', () => {
  user.value = null; activeSession.value = null
  if (router.currentRoute.value.name !== 'login') router.replace({ name: 'login', query: { redirect: router.currentRoute.value.fullPath } })
})
export default router
