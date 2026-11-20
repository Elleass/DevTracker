<script setup>
import { ref, onMounted } from 'vue'
import { api } from '@/api/client'
import ApiFeedback from '@/components/ApiFeedback.vue'
const users = ref([]), error = ref(null)
onMounted(async () => { try { users.value = await api('/admin/users') } catch (e) { error.value = e } })
</script><template><h1>Użytkownicy</h1><ApiFeedback :error="error" /><section class="panel table-scroll"><table><thead><tr><th>E-mail</th><th>Pseudonim</th><th>Role</th></tr></thead><tbody><tr v-for="user in users" :key="user.id"><td>{{ user.email }}</td><td>{{ user.nickname }}</td><td>{{ user.roles.join(', ') }}</td></tr></tbody></table></section></template>
