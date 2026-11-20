<script setup>
import { ref, watch, computed, onMounted, onBeforeUnmount } from 'vue'
import { onBeforeRouteLeave } from 'vue-router'
import { renderMarkdown } from '@/lib/markdown'
import { api } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import AppDialog from './AppDialog.vue'
import ConfirmAction from './ConfirmAction.vue'
import ApiFeedback from './ApiFeedback.vue'
const props = defineProps({ projectId: String })
const documents = ref([]), error = ref(null), editing = ref(null), open = ref(false), title = ref(''), content = ref(''), busy = ref(false), deleting = ref(null), selected = ref(null), formError = ref(null)
const root = () => `/projects/${props.projectId}/documents`
async function load() { try { documents.value = await api(root()); error.value = null } catch (e) { error.value = e } }
watch(() => props.projectId, load, { immediate: true })
function edit(document) { editing.value = document; title.value = document?.title || ''; content.value = document?.contentMarkdown || ''; formError.value = null; open.value = true }
const preview = computed(() => renderMarkdown(selected.value?.contentMarkdown))
async function save() { busy.value = true; try { const saved = await api(editing.value ? `${root()}/${editing.value.id}` : root(), { method: editing.value ? 'PUT' : 'POST', body: { title: title.value, contentMarkdown: content.value } }); selected.value = saved; open.value = false; await load() } catch (e) { formError.value = e } finally { busy.value = false } }
async function remove() { try { await api(`${root()}/${deleting.value.id}`, { method: 'DELETE' }); if (selected.value?.id === deleting.value.id) selected.value = null; await load() } catch (e) { error.value = e } finally { deleting.value = null } }
function close(value) { if (value || (title.value === (editing.value?.title || '') && content.value === (editing.value?.contentMarkdown || '')) || window.confirm('Odrzucić niezapisane zmiany dokumentu?')) open.value = value }
const dirty = () => open.value && (title.value !== (editing.value?.title || '') || content.value !== (editing.value?.contentMarkdown || ''))
onBeforeRouteLeave(() => !dirty() || window.confirm('Odrzucić niezapisane zmiany dokumentu?'))
function beforeUnload(event) { if (dirty()) { event.preventDefault(); event.returnValue = '' } }
onMounted(() => window.addEventListener('beforeunload', beforeUnload))
onBeforeUnmount(() => window.removeEventListener('beforeunload', beforeUnload))
</script>
<template>
  <section class="panel"><div class="toolbar"><h2>Dokumentacja</h2><Button @click="edit(null)">Nowy dokument</Button></div><ApiFeedback :error="error" /><p v-if="!documents.length" class="muted">Zapisz notatki i dokumentację projektu w Markdown.</p>
    <div v-for="document in documents" :key="document.id" class="document-row"><button class="link" @click="selected = document">{{ document.title }}</button><div class="actions"><Button variant="outline" @click="edit(document)">Edytuj dokument</Button><Button variant="ghost" @click="deleting = document">Usuń dokument</Button></div></div>
    <article v-if="selected" class="markdown"><h3>{{ selected.title }}</h3><div v-html="preview" /></article>
  </section>
  <AppDialog :open="open" :title="editing ? 'Edytuj dokument' : 'Nowy dokument'" @update:open="close"><form @submit.prevent="save" class="form-stack"><ApiFeedback :error="formError" /><label for="doc-title">Tytuł dokumentu</label><Input id="doc-title" v-model="title" required maxlength="150" /><label for="doc-content">Treść Markdown</label><Textarea id="doc-content" v-model="content" maxlength="100000" rows="12" /><Button :disabled="busy" type="submit">Zapisz dokument</Button></form></AppDialog>
  <ConfirmAction :open="!!deleting" :text="`Usunąć dokument ${deleting?.title || ''}?`" @update:open="!$event && (deleting = null)" @confirm="remove" />
</template>
