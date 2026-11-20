<script setup>
import { useRoute } from 'vue-router';
import { ref, computed, watch } from 'vue'

const route = useRoute();
const projectId = computed(() => route.params.projectId)
const tasks = ref([])

async function loadTasks(projectId) {
    try {

        const response = await fetch(`api/projects/${projectId}/tasks`);
        console.log("Status:", response.status)
        console.log("OK:", response.ok)
        if (response.ok) {
            const data = await response.json()
            tasks.value = data
        }

    }
    catch {
        console.log("failed to fetch tasks")
    }
}

watch(projectId, (newProjectId) => {
    loadTasks(newProjectId)
}, { immediate: true })
</script>

<template>
    <h1>{{ route.params.projectId }}</h1>

    <div v-for="task in tasks" :key="task.id">
        <p>{{ task.title }}</p>
    </div>
</template>