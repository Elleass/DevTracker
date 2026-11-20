<script setup>
import { ref, computed } from 'vue'
import { projects, formatDuration } from '@/composables/workspace'
import { projectState, projectStates } from '@/lib/project-status'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import ProjectForm from '@/components/ProjectForm.vue'
import ProjectStatus from '@/components/ProjectStatus.vue'
const open = ref(false), status = ref(''), search = ref('')
const filtered = computed(() => projects.value.filter(project =>
  (!status.value || projectState(project).value === status.value) &&
  project.name.toLocaleLowerCase('en').includes(search.value.trim().toLocaleLowerCase('en')),
))
</script>
<template>
  <div class="page-heading">
    <div><p class="eyebrow">WORKSPACE</p><h1>My projects</h1><p class="muted">From the first idea to the final task.</p></div>
    <Button @click="open = true">New project</Button>
  </div>
  <div class="toolbar filters project-filters">
    <label>Search projects<Input v-model="search" type="search" /></label>
    <label>Project status<select v-model="status"><option value="">All statuses</option><option v-for="state in projectStates" :key="state.value" :value="state.value">{{ state.label }}</option></select></label>
    <span class="muted" role="status">{{ filtered.length }} / {{ projects.length }} projects</span>
  </div>
  <p v-if="!projects.length" class="panel muted">No projects yet. Create your first project to get started.</p>
  <p v-else-if="!filtered.length" class="panel muted">No projects match these filters.</p>
  <div class="project-grid">
    <RouterLink v-for="project in filtered" :key="project.id" :to="`/projects/${project.id}`" class="panel project-card">
      <ProjectStatus :project="project" /><h2>{{ project.name }}</h2>
      <p class="muted">{{ project.description || 'Add a description for your project.' }}</p>
      <span class="metric">Work time {{ formatDuration(project.totalWorkedSeconds) }}</span>
    </RouterLink>
  </div>
  <ProjectForm v-model:open="open" />
</template>
