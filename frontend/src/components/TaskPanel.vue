<script setup>
import { ref, watch } from 'vue'
import { api } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import AppDialog from './AppDialog.vue'
import ConfirmAction from './ConfirmAction.vue'
import ApiFeedback from './ApiFeedback.vue'
const props = defineProps({ projectId: String, archived: Boolean })
const page = ref(1), search = ref(''), status = ref(''), data = ref({ items: [], totalPages: 0 }), loading = ref(false), error = ref(null)
const editing = ref(null), open = ref(false), busy = ref(false), title = ref(''), description = ref(''), taskStatus = ref(0), due = ref(''), formError = ref(null), deleting = ref(null)
const dependencyTask = ref(null), links = ref([]), candidates = ref([]), candidateSearch = ref('')
let request
const root = () => `/projects/${props.projectId}/tasks`
async function load(reset = false) {
  if (reset) page.value = 1
  request?.abort(); request = new AbortController(); const current = request
  loading.value = true; error.value = null
  try {
    const query = new URLSearchParams({ pageNumber: page.value, pageSize: 20, search: search.value })
    if (status.value !== '') query.set('status', status.value)
    const result = await api(`${root()}?${query}`, { signal: current.signal })
    if (current !== request) return
    data.value = result
    if (!result.items.length && page.value > 1) { page.value--; return load() }
  } catch (e) { if (e.name !== 'AbortError') error.value = e } finally { if (current === request) loading.value = false }
}
watch(() => props.projectId, () => load(true), { immediate: true })
function edit(task) {
  editing.value = task; title.value = task?.title || ''; description.value = task?.description || ''; taskStatus.value = task?.status ?? 0
  due.value = task?.dueAt ? new Date(new Date(task.dueAt) - new Date(task.dueAt).getTimezoneOffset() * 60000).toISOString().slice(0, 16) : ''
  formError.value = null; open.value = true
}
async function save() {
  busy.value = true; formError.value = null
  try {
    await api(editing.value ? `${root()}/${editing.value.id}` : root(), { method: editing.value ? 'PATCH' : 'POST', body: { title: title.value, description: description.value || null, status: Number(taskStatus.value), dueAt: due.value ? new Date(due.value).toISOString() : null } })
    open.value = false; await load()
  } catch (e) { formError.value = e } finally { busy.value = false }
}
async function remove() { try { await api(`${root()}/${deleting.value.id}`, { method: 'DELETE' }); await load() } catch (e) { error.value = e } finally { deleting.value = null } }
async function loadDependencies(task) {
  dependencyTask.value = task; formError.value = null
  try {
    const values = await api(`${root()}/${task.id}/dependencies`)
    links.value = await Promise.all(values.map(async link => ({ ...link, task: await api(`${root()}/${link.predecessorTaskId}`) })))
    await findCandidates()
  } catch (e) { formError.value = e }
}
async function findCandidates() { try { candidates.value = (await api(`${root()}?pageSize=100&search=${encodeURIComponent(candidateSearch.value)}`)).items.filter(t => t.id !== dependencyTask.value?.id && !links.value.some(link => link.predecessorTaskId === t.id)) } catch (e) { formError.value = e } }
async function changeDependency(id, remove = false) {
  try { await api(`${root()}/${dependencyTask.value.id}/dependencies${remove ? `/${id}` : ''}`, { method: remove ? 'DELETE' : 'POST', ...(remove ? {} : { body: { predecessorTaskId: id } }) }); await loadDependencies(dependencyTask.value) }
  catch (e) { formError.value = e }
}
</script>
<template>
  <section class="panel"><div class="toolbar"><h2>Zadania</h2><Button :disabled="archived" @click="edit(null)">Dodaj zadanie</Button></div>
    <form class="toolbar filters" @submit.prevent="load(true)"><label>Szukaj zadania<Input v-model="search" type="search" maxlength="150" /></label><label>Status<select v-model="status"><option value="">Wszystkie</option><option value="0">Do zrobienia</option><option value="1">W trakcie</option><option value="2">Zakończone</option></select></label><Button type="submit" variant="secondary">Filtruj</Button></form>
    <ApiFeedback :error="error" :loading="loading" />
    <p v-if="!loading && !data.items.length" class="muted">Brak pasujących zadań.</p>
    <div class="table-scroll"><table v-if="data.items.length"><thead><tr><th>Tytuł</th><th>Status</th><th>Termin</th><th>Akcje</th></tr></thead><tbody><tr v-for="task in data.items" :key="task.id"><td><strong>{{ task.title }}</strong><p class="muted">{{ task.description }}</p></td><td>{{ ['Do zrobienia', 'W trakcie', 'Zakończone'][task.status] }}</td><td>{{ task.dueAt ? new Date(task.dueAt).toLocaleString('pl-PL') : '—' }}</td><td class="actions"><Button variant="outline" :disabled="archived" @click="edit(task)">Edytuj</Button><Button variant="outline" @click="loadDependencies(task)">Poprzedniki</Button><Button variant="ghost" @click="deleting = task">Usuń</Button></td></tr></tbody></table></div>
    <nav aria-label="Strony zadań" class="toolbar"><Button variant="outline" :disabled="page <= 1 || loading" @click="page--; load()">Poprzednia</Button><span>Strona {{ page }} / {{ Math.max(1, data.totalPages) }} · {{ data.totalCount || 0 }} zadań</span><Button variant="outline" :disabled="!data.hasNextPage || loading" @click="page++; load()">Następna</Button></nav>
  </section>
  <AppDialog v-model:open="open" :title="editing ? 'Edytuj zadanie' : 'Nowe zadanie'"><form class="form-stack" @submit.prevent="save"><ApiFeedback :error="formError" /><label for="task-title">Tytuł zadania</label><Input id="task-title" v-model="title" required maxlength="150" /><label for="task-description">Opis zadania</label><Textarea id="task-description" v-model="description" maxlength="4000" /><template v-if="editing"><label for="task-status">Status zadania</label><select id="task-status" v-model="taskStatus"><option :value="0">Do zrobienia</option><option :value="1">W trakcie</option><option :value="2">Zakończone</option></select></template><label for="task-due">Termin zadania</label><Input id="task-due" v-model="due" type="datetime-local" /><Button type="submit" :disabled="busy">Zapisz zadanie</Button></form></AppDialog>
  <AppDialog :open="!!dependencyTask" title="Wymagane poprzedniki" description="Zadanie można zakończyć dopiero po ukończeniu wszystkich poprzedników." @update:open="!$event && (dependencyTask = null)"><ApiFeedback :error="formError" /><ul><li v-for="link in links" :key="link.predecessorTaskId" class="toolbar">{{ link.task.title }}<Button variant="ghost" :disabled="archived" @click="changeDependency(link.predecessorTaskId, true)">Odłącz</Button></li></ul><form @submit.prevent="findCandidates" class="toolbar"><Input v-model="candidateSearch" placeholder="Wyszukaj poprzednik" aria-label="Wyszukaj poprzednik" /><Button type="submit" variant="outline">Szukaj</Button></form><div class="candidate-list"><div v-for="task in candidates" :key="task.id" class="toolbar"><span>{{ task.title }}</span><Button variant="outline" :disabled="archived" @click="changeDependency(task.id)">Dodaj poprzednik</Button></div></div></AppDialog>
  <ConfirmAction :open="!!deleting" :text="`Usunąć zadanie ${deleting?.title || ''}?`" @update:open="!$event && (deleting = null)" @confirm="remove" />
</template>
