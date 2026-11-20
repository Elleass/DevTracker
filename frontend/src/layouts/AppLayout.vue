<script setup>
import ProjectsNavigation from '@/components/navigation/ProjectsNavigation.vue';
import { ref, onMounted } from 'vue'


const projects = ref([]);
async function loadProjects()
{
    const response = await fetch('/api/projects')
    console.log(response.status)
    const data = await response.json()
    console.log(data.projects)
    projects.value = data.projects;
}

onMounted(() => {
    loadProjects();
})



</script>
<template>
    <div class="flex min-h-screen">

        <aside class="w-sidebar bg-sidebar shrink-0 ">
            <nav>
                <ProjectsNavigation :projects="projects" />

            </nav>
        </aside>

        <main class="flex-1 min-w-0">
            <RouterView />

        </main>
    </div>





</template>