<script setup>
import { ref, watch } from 'vue'
import { api } from '@/api/client'
import { refreshProjects } from '@/composables/workspace'
import AppDialog from './AppDialog.vue'
import ApiFeedback from './ApiFeedback.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
const props = defineProps({ open: Boolean, project: Object })
const emit = defineEmits(['update:open', 'saved'])
const name = ref(''), description = ref(''), status = ref(0), error = ref(null), busy = ref(false)
watch(() => props.open, value => { if (value) { name.value = props.project?.name || ''; description.value = props.project?.description || ''; status.value = props.project?.status ?? 0; error.value = null } })
async function save() {
  busy.value = true; error.value = null
  try {
    await api(props.project ? `/projects/${props.project.id}` : '/projects', { method: props.project ? 'PATCH' : 'POST', body: { name: name.value, description: description.value || null, projectStatus: Number(status.value) } })
    await refreshProjects(); emit('saved'); emit('update:open', false)
  } catch (e) { error.value = e } finally { busy.value = false }
}
</script>
<template>
  <AppDialog :open="open" :title="project ? 'Edytuj projekt' : 'Nowy projekt'" @update:open="emit('update:open', $event)">
    <form @submit.prevent="save" class="form-stack"><ApiFeedback :error="error" />
      <label for="project-name">Nazwa projektu</label><Input id="project-name" v-model="name" required maxlength="150" />
      <label for="project-description">Opis projektu</label><Textarea id="project-description" v-model="description" maxlength="4000" />
      <template v-if="project"><label for="project-status">Status projektu</label><select id="project-status" v-model="status"><option :value="0">Pomysł</option><option :value="1">Aktywny</option><option :value="2">Wstrzymany</option><option :value="3">Ukończony</option></select></template>
      <Button :disabled="busy" type="submit">{{ busy ? 'Zapisywanie…' : 'Zapisz projekt' }}</Button>
    </form>
  </AppDialog>
</template>
