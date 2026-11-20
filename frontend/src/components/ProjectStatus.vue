<script setup>
import { projectState } from '@/lib/project-status'
defineProps({ project: { type: Object, required: true } })
</script>

<template>
  <span class="status-badge" :data-state="projectState(project).value">{{ projectState(project).label }}</span>
</template>
