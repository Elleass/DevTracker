<script setup>
defineProps({ projects: Array })
</script>
<template><nav aria-label="Projects"><RouterLink v-for="project in projects" :key="project.id" :to="`/projects/${project.id}`" class="project-link">{{ project.name }}</RouterLink></nav></template>
