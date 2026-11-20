<script setup>
import { ref, watch, onMounted, onBeforeUnmount } from 'vue'
import { api } from '@/api/client'
import { activeSession, refreshSession, formatDuration } from '@/composables/workspace'
import { Button } from '@/components/ui/button'
import ApiFeedback from './ApiFeedback.vue'
const props = defineProps({ projectId: String, archived: Boolean })
const history = ref({ items: [] }), page = ref(1), error = ref(null), busy = ref(false)
async function load() { try { history.value = await api(`/work-sessions?pageNumber=${page.value}${props.projectId ? `&projectId=${props.projectId}` : ''}`); error.value = null } catch (e) { error.value = e } }
watch(() => props.projectId, () => { page.value = 1; load() }, { immediate: true })
async function start() { busy.value = true; try { activeSession.value = await api(`/projects/${props.projectId}/work-sessions`, { method: 'POST' }); await load() } catch (e) { error.value = e; await refreshSession().catch(() => {}) } finally { busy.value = false } }
onMounted(() => window.addEventListener('work-stopped', load)); onBeforeUnmount(() => window.removeEventListener('work-stopped', load))
</script>
<template><section class="panel"><div class="toolbar"><h2>Sesje pracy</h2><Button v-if="projectId" :disabled="busy || archived || !!activeSession" @click="start">Rozpocznij pracę</Button></div><p v-if="activeSession" class="muted">Masz aktywną sesję — zakończ ją w pasku aplikacji przed rozpoczęciem kolejnej.</p><ApiFeedback :error="error" /><p v-if="!history.items.length" class="muted">Brak sesji pracy.</p><div class="table-scroll"><table v-if="history.items.length"><thead><tr><th>Rozpoczęcie</th><th>Zakończenie</th><th>Czas</th></tr></thead><tbody><tr v-for="session in history.items" :key="session.id"><td>{{ new Date(session.startedAt).toLocaleString('pl-PL') }}</td><td>{{ session.endedAt ? new Date(session.endedAt).toLocaleString('pl-PL') : 'Aktywna' }}</td><td>{{ formatDuration(session.durationSeconds) }}</td></tr></tbody></table></div><nav class="toolbar" aria-label="Strony historii sesji"><Button variant="outline" :disabled="page <= 1" @click="page--; load()">Poprzednia</Button><span>Strona {{ page }} / {{ Math.max(1, history.totalPages || 0) }}</span><Button variant="outline" :disabled="!history.hasNextPage" @click="page++; load()">Następna</Button></nav></section></template>
