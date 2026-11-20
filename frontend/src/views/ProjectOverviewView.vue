<script setup>
import { ref, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '@/api/client'
import { refreshProjects, formatDuration } from '@/composables/workspace'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import ProjectForm from '@/components/ProjectForm.vue'
import ConfirmAction from '@/components/ConfirmAction.vue'
import ApiFeedback from '@/components/ApiFeedback.vue'
import TaskPanel from '@/components/TaskPanel.vue'
import DocumentPanel from '@/components/DocumentPanel.vue'
import SessionPanel from '@/components/SessionPanel.vue'
const route = useRoute(), router = useRouter(), project = ref(null), error = ref(null), editing = ref(false), confirmation = ref(null), tab = ref('tasks')
let generation = 0
async function load() { const current = ++generation; try { const value = (await api(`/projects/${route.params.projectId}`)).project; if (current === generation) { project.value = value; error.value = null } } catch (e) { if (current === generation) error.value = e } }
watch(() => route.params.projectId, () => { project.value = null; load() }, { immediate: true })
function refreshStats() { setTimeout(load, 1500) }
onMounted(() => window.addEventListener('work-stopped', refreshStats)); onBeforeUnmount(() => window.removeEventListener('work-stopped', refreshStats))
async function confirm() { const operation = confirmation.value; if (!['archive', 'delete'].includes(operation)) return; confirmation.value = null; try { await api(`/projects/${project.value.id}${operation === 'archive' ? '/archive' : ''}`, { method: operation === 'archive' ? 'POST' : 'DELETE' }); await refreshProjects(); if (operation === 'delete') router.replace('/projects'); else await load() } catch (e) { error.value = e } }
</script>
<template><ApiFeedback :error="error" :loading="!project && !error" /><template v-if="project"><div class="page-heading"><div><p class="eyebrow">PROJEKT</p><h1>{{ project.name }}</h1><p class="muted">{{ project.description }}</p><Badge>{{ project.archivedAt ? 'Zarchiwizowany' : ['Pomysł','Aktywny','Wstrzymany','Ukończony'][project.status] }}</Badge><p class="metric">Łączny czas pracy: {{ formatDuration(project.totalWorkedSeconds) }}</p></div><div class="actions"><Button variant="outline" @click="editing = true">Edytuj projekt</Button><Button variant="outline" :disabled="!!project.archivedAt" @click="confirmation = 'archive'">Archiwizuj</Button><Button variant="ghost" @click="confirmation = 'delete'">Usuń projekt</Button></div></div><nav class="toolbar tabs" aria-label="Sekcje projektu"><Button v-for="item in [['tasks','Zadania'],['documents','Dokumentacja'],['sessions','Sesje pracy']]" :key="item[0]" :variant="tab === item[0] ? 'default' : 'ghost'" @click="tab = item[0]">{{ item[1] }}</Button></nav><TaskPanel v-if="tab === 'tasks'" :key="project.id" :project-id="project.id" :archived="!!project.archivedAt" /><DocumentPanel v-if="tab === 'documents'" :key="project.id" :project-id="project.id" /><SessionPanel v-if="tab === 'sessions'" :key="project.id" :project-id="project.id" :archived="!!project.archivedAt" /><ProjectForm v-model:open="editing" :project="project" @saved="load" /><ConfirmAction :open="!!confirmation" :text="confirmation === 'archive' ? 'Archiwizacja wymaga zakończenia wszystkich zadań. Kontynuować?' : 'Usunąć projekt, zadania, dokumenty i całą historię pracy? Tej operacji nie można cofnąć.'" @update:open="!$event && (confirmation = null)" @confirm="confirm" /></template></template>
