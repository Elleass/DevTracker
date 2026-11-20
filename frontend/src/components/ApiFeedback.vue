<script setup>
defineProps({ error: Object, loading: Boolean })
</script>
<template>
  <p v-if="loading" role="status" class="muted">Ładowanie…</p>
  <div v-if="error" role="alert" class="error-box">
    <strong>{{ error.status === 403 ? 'Brak uprawnień.' : error.status === 404 ? 'Nie znaleziono zasobu.' : error.message || 'Brak połączenia z serwerem.' }}</strong>
    <ul v-if="error.fields"><li v-for="(messages, field) in error.fields" :key="field">{{ field }}: {{ messages.join(' ') }}</li></ul>
    <small v-if="error.traceId">Identyfikator błędu: {{ error.traceId }}</small>
  </div>
</template>
