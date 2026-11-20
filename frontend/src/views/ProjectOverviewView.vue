<script setup>
import { useRoute } from 'vue-router';
import { ref, computed, watch, onBeforeUnmount } from 'vue'

const route = useRoute();
const projectId = computed(() => route.params.projectId)
const tasks = ref([])
const search = ref('')
const status = ref('')
const pageNumber = ref(1)
const totalPages = ref(0)
const totalCount = ref(0)
const isLoading = ref(false)
const errorMessage = ref('')
let activeRequest
let appliedFilters = { search: '', status: '' }

async function loadTasks() {
    activeRequest?.abort()
    const request = new AbortController()
    activeRequest = request
    isLoading.value = true
    errorMessage.value = ''
    try {
        const query = new URLSearchParams({ pageNumber: pageNumber.value, pageSize: 20 })
        if (appliedFilters.search) query.set('search', appliedFilters.search)
        if (appliedFilters.status !== '') query.set('status', appliedFilters.status)
        const response = await fetch(`/api/projects/${encodeURIComponent(projectId.value)}/tasks?${query}`, {
            signal: request.signal,
        })
        if (!response.ok) {
            tasks.value = []
            totalPages.value = 0
            totalCount.value = 0
            errorMessage.value = response.status === 401 ? 'Sesja wygasła. Zaloguj się ponownie.' : 'Nie udało się pobrać zadań.'
            return
        }
        const data = await response.json()
        tasks.value = data.items
        totalPages.value = data.totalPages
        totalCount.value = data.totalCount
    }
    catch (error) {
        if (error.name !== 'AbortError') {
            tasks.value = []
            totalPages.value = 0
            totalCount.value = 0
            errorMessage.value = 'Brak połączenia z serwerem.'
        }
    } finally {
        if (activeRequest === request) isLoading.value = false
    }
}

function applyFilters() {
    pageNumber.value = 1
    appliedFilters = { search: search.value.trim(), status: status.value }
    loadTasks()
}

function changePage(page) {
    pageNumber.value = page
    loadTasks()
}

watch(projectId, () => {
    pageNumber.value = 1
    tasks.value = []
    loadTasks()
}, { immediate: true })
onBeforeUnmount(() => activeRequest?.abort())
</script>

<template>
    <h1>{{ route.params.projectId }}</h1>
    <form @submit.prevent="applyFilters" class="flex gap-3 items-end my-4">
        <label>Wyszukaj po tytule
            <input v-model="search" maxlength="150" type="search" class="border p-2 block" />
        </label>
        <label>Status
            <select v-model="status" class="border p-2 block">
                <option value="">Wszystkie</option>
                <option value="0">Do zrobienia</option>
                <option value="1">W trakcie</option>
                <option value="2">Zakończone</option>
            </select>
        </label>
        <button type="submit" :disabled="isLoading" class="border p-2">Filtruj</button>
    </form>
    <p v-if="errorMessage" role="alert">{{ errorMessage }}</p>
    <p v-else-if="isLoading" role="status">Ładowanie…</p>
    <p v-else-if="!tasks.length">Brak pasujących zadań na tej stronie.</p>
    <div v-for="task in tasks" :key="task.id">
        <p>{{ task.title }}</p>
    </div>
    <nav aria-label="Strony zadań" class="flex gap-3 items-center mt-4">
        <button :disabled="isLoading || pageNumber <= 1" @click="changePage(pageNumber - 1)" class="border p-2">Poprzednia</button>
        <span>Strona {{ pageNumber }} z {{ Math.max(totalPages, 1) }} · Zadania: {{ totalCount }}</span>
        <button :disabled="isLoading || pageNumber >= totalPages" @click="changePage(pageNumber + 1)" class="border p-2">Następna</button>
    </nav>
</template>
