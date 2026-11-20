<script setup>
import PanelMenu from 'primevue/panelmenu'

import { computed } from 'vue'



const props = defineProps({
    projects: {
        type: Array,
        required: true
    }
})


const items = computed(() => [
    {
        key: 'projects',
        label: 'Projekty',
        icon: 'pi pi-folder',
        items: props.projects.map(project => ({
            key: project.id,
            label: project.name,
            route: {
                name: 'project-overview',
                params: { projectId: project.id }
            }
        }))
    }
])

</script>

<template>
    <PanelMenu :model="items">
        <template #item="{ item }">
            <RouterLink v-if="item.route" :to="item.route">
                {{ item.label }}
            </RouterLink>

            <span v-else>
                {{ item.label }}
            </span>
        </template>
    </PanelMenu>
</template>