import { createRouter, createWebHistory } from 'vue-router'
import ProjectView from '@/views/ProjectView.vue'
import ProjectOverviewView from '@/views/ProjectOverviewView.vue';
import LoginView from '@/views/LoginView.vue';
import AppLayout from '@/layouts/AppLayout.vue';



const routes = [
  {
    path: '/projects',
    component: AppLayout,
    meta: {requiresAuth: true},
    children: [
      {
        path: '',
        name: 'projects',
        component: ProjectView
      },
      {
        path: ':projectId',
        name: 'project-overview',
        component: ProjectOverviewView
      }
    ]
  },
  {  
    path: '/login',
    name: 'login',
    component: LoginView
  }

]



const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes
})

router.beforeEach(async (to) => {
  if (!to.meta.requiresAuth) {
    return true
  }

  try {
    const response = await fetch('/api/auth/me')

    if (response.status === 401) {
      return {
        name: 'login',
        query: { redirect: to.fullPath }
      }
    }

    if (!response.ok) {
      return false
    }

    return true
  } catch (error) {
    console.error('Session check failed:', error)
    return false
  }
})

export default router;
