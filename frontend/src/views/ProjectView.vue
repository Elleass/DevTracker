<script setup>
import { ref } from 'vue'
import { projects, formatDuration } from '@/composables/workspace'
import { Button } from '@/components/ui/button'
import ProjectForm from '@/components/ProjectForm.vue'
const open = ref(false)
</script>
<template><div class="page-heading"><div><p class="eyebrow">PRZESTRZEŃ ROBOCZA</p><h1>Moje projekty</h1><p class="muted">Od pierwszego pomysłu do ostatniego zadania.</p></div><Button @click="open = true">Nowy projekt</Button></div><p v-if="!projects.length" class="panel muted">Nie masz jeszcze projektów. Utwórz pierwszy, aby zacząć.</p><div class="project-grid"><RouterLink v-for="project in projects" :key="project.id" :to="`/projects/${project.id}`" class="panel project-card"><span class="eyebrow">{{ project.archivedAt ? 'ZARCHIWIZOWANY' : ['POMYSŁ','AKTYWNY','WSTRZYMANY','UKOŃCZONY'][project.status] }}</span><h2>{{ project.name }}</h2><p class="muted">{{ project.description || 'Dodaj opis swojego projektu.' }}</p><span class="metric">Czas pracy {{ formatDuration(project.totalWorkedSeconds) }}</span></RouterLink></div><ProjectForm v-model:open="open" /></template>
