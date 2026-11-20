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
const editing = ref(null), open = ref(false), busy = ref(false), title = ref(''), description = ref(''), taskStatus = ref(0), estimate = ref(''), priority = ref(1), formError = ref(null), deleting = ref(null)
const dependencyTask = ref(null), links = ref([]), candidates = ref([]), candidateSearch = ref('')
function estimateLabel(minutes) { if (!minutes) return '—'; const hours = Math.floor(minutes / 60), rest = minutes % 60; return [hours ? `${hours} h` : '', rest ? `${rest} min` : ''].filter(Boolean).join(' ') }
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
  estimate.value = task?.estimatedMinutes ?? ''; priority.value = task?.priority ?? 1
  formError.value = null; open.value = true
}
async function save() {
  busy.value = true; formError.value = null
  try {
    await api(editing.value ? `${root()}/${editing.value.id}` : root(), { method: editing.value ? 'PATCH' : 'POST', body: { title: title.value, description: description.value || null, status: Number(taskStatus.value), estimatedMinutes: estimate.value === '' ? null : Number(estimate.value), priority: Number(priority.value) } })
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
  <section class="panel"><div class="toolbar"><h2>Tasks</h2><Button :disabled="archived" @click="edit(null)">Add task</Button></div>
    <form class="toolbar filters" @submit.prevent="load(true)"><label>Search tasks<Input v-model="search" type="search" maxlength="150" /></label><label>Status<select v-model="status"><option value="">All</option><option value="0">To do</option><option value="1">In progress</option><option value="2">Done</option></select></label><Button type="submit" variant="secondary">Filter</Button></form>
    <ApiFeedback :error="error" :loading="loading" />
    <p v-if="!loading && !data.items.length" class="muted">No matching tasks.</p>
    <div class="table-scroll"><table v-if="data.items.length" class="task-table"><thead><tr><th>Title</th><th>Status</th><th>Estimate</th><th>Priority</th><th>Actions</th></tr></thead><tbody><tr v-for="task in data.items" :key="task.id"><td class="task-title"><strong>{{ task.title }}</strong><p class="muted">{{ task.description }}</p></td><td class="task-meta">{{ ['To do', 'In progress', 'Done'][task.status] }}</td><td class="task-meta">{{ estimateLabel(task.estimatedMinutes) }}</td><td><span class="status-badge" :data-state="['low','normal','high'][task.priority ?? 1]">{{ ['Low','Normal','High'][task.priority ?? 1] }}</span></td><td><div class="actions task-actions"><Button variant="outline" :disabled="archived" @click="edit(task)">Edit</Button><Button variant="outline" @click="loadDependencies(task)">Predecessors</Button><Button variant="ghost" @click="deleting = task">Delete</Button></div></td></tr></tbody></table></div>
    <nav aria-label="Task pages" class="toolbar"><Button variant="outline" :disabled="page <= 1 || loading" @click="page--; load()">Previous</Button><span>Page {{ page }} / {{ Math.max(1, data.totalPages) }} · {{ data.totalCount || 0 }} tasks</span><Button variant="outline" :disabled="!data.hasNextPage || loading" @click="page++; load()">Next</Button></nav>
  </section>
  <AppDialog v-model:open="open" :title="editing ? 'Edit task' : 'New task'"><form class="form-stack" @submit.prevent="save"><ApiFeedback :error="formError" /><label for="task-title">Task title</label><Input id="task-title" v-model="title" required maxlength="150" /><label for="task-description">Task description</label><Textarea id="task-description" v-model="description" maxlength="4000" /><template v-if="editing"><label for="task-status">Task status</label><select id="task-status" v-model="taskStatus"><option :value="0">To do</option><option :value="1">In progress</option><option :value="2">Done</option></select></template><label for="task-estimate">Estimated time (minutes)</label><Input id="task-estimate" v-model="estimate" type="number" min="1" max="525600" step="1" placeholder="Optional" /><label for="task-priority">Priority</label><select id="task-priority" v-model="priority"><option :value="0">Low</option><option :value="1">Normal</option><option :value="2">High</option></select><Button type="submit" :disabled="busy">Save task</Button></form></AppDialog>
  <AppDialog :open="!!dependencyTask" title="Required predecessors" description="Complete all required predecessors before completing this task." @update:open="!$event && (dependencyTask = null)"><ApiFeedback :error="formError" /><ul><li v-for="link in links" :key="link.predecessorTaskId" class="toolbar">{{ link.task.title }}<Button variant="ghost" :disabled="archived" @click="changeDependency(link.predecessorTaskId, true)">Unlink</Button></li></ul><form @submit.prevent="findCandidates" class="toolbar"><Input v-model="candidateSearch" placeholder="Search predecessors" aria-label="Search predecessors" /><Button type="submit" variant="outline">Search</Button></form><div class="candidate-list"><div v-for="task in candidates" :key="task.id" class="toolbar"><span>{{ task.title }}</span><Button variant="outline" :disabled="archived" @click="changeDependency(task.id)">Add predecessor</Button></div></div></AppDialog>
  <ConfirmAction :open="!!deleting" :text="`Delete task ${deleting?.title || ''}?`" @update:open="!$event && (deleting = null)" @confirm="remove" />
</template>
