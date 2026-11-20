<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '@/api/client'
import { user, projects, activeSession, refreshProjects, refreshSession, logout, formatDuration } from '@/composables/workspace'
import { Button } from '@/components/ui/button'
import ProjectsNavigation from '@/components/navigation/ProjectsNavigation.vue'
import ApiFeedback from '@/components/ApiFeedback.vue'
const router = useRouter(), error = ref(null), now = ref(Date.now()), stopping = ref(false)
const ticker = setInterval(() => { now.value = Date.now() }, 1000)
onBeforeUnmount(() => clearInterval(ticker))
const elapsed = computed(() => activeSession.value ? formatDuration((now.value - new Date(activeSession.value.startedAt)) / 1000) : '')
onMounted(async () => { try { await Promise.all([refreshProjects(), refreshSession()]) } catch (e) { error.value = e } })
async function stop() { stopping.value = true; try { await api(`/work-sessions/${activeSession.value.id}/stop`, { method: 'POST' }); activeSession.value = null; window.dispatchEvent(new Event('work-stopped')); setTimeout(() => refreshProjects().catch(() => {}), 1000) } catch (e) { error.value = e } finally { stopping.value = false } }
async function signOut() { try { await logout(); await router.replace('/login') } catch (e) { error.value = e } }
</script>
<template><div class="app-shell"><aside class="sidebar"><RouterLink to="/projects" class="brand">DEVTRACKER<span>Your project workspace</span></RouterLink><RouterLink class="nav-link" to="/projects">All projects</RouterLink><RouterLink class="nav-link" to="/sessions">Work history</RouterLink><RouterLink v-if="user?.roles?.includes('ADMIN')" class="nav-link" to="/admin">Users</RouterLink><h2 class="eyebrow">MY PROJECTS</h2><ProjectsNavigation :projects="projects" /></aside><div class="app-body"><header class="topbar"><span>{{ user?.nickname }}</span><div v-if="activeSession" class="toolbar"><RouterLink :to="`/projects/${activeSession.projectId}`" class="link">Active session · {{ elapsed }}</RouterLink><Button variant="outline" :disabled="stopping" @click="stop">Stop session</Button></div><Button variant="ghost" @click="signOut">Sign out</Button></header><main class="page"><ApiFeedback :error="error" /><RouterView /></main></div></div></template>
