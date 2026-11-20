<script setup>
defineProps({ error: Object, loading: Boolean, showTrace: { type: Boolean, default: true } })
</script>
<template>
  <p v-if="loading" role="status" class="muted">Loading…</p>
  <div v-if="error" role="alert" class="error-box">
    <strong>{{ error.status === 403 ? 'Access denied.' : error.status === 404 ? 'Resource not found.' : error.message || 'Unable to connect to the server.' }}</strong>
    <ul v-if="error.fields"><li v-for="(messages, field) in error.fields" :key="field">{{ field }}: {{ messages.join(' ') }}</li></ul>
    <small v-if="showTrace && error.traceId">Error ID: {{ error.traceId }}</small>
  </div>
</template>
