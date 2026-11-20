<script setup>
import { ref, onBeforeUnmount } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { api } from '@/api/client'
import { checkSession } from '@/composables/workspace'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import ApiFeedback from '@/components/ApiFeedback.vue'
const router = useRouter(), route = useRoute()
const email = ref(''), password = ref(''), nickname = ref(''), registration = ref(false), error = ref(null), busy = ref(false), visible = ref(false), cooldown = ref(0)
const ticker = setInterval(() => { if (cooldown.value > 0) cooldown.value-- }, 1000)
onBeforeUnmount(() => clearInterval(ticker))
async function submit() {
  if (busy.value || cooldown.value) return
  busy.value = true; error.value = null
  try {
    if (registration.value) await api('/auth/register', { method: 'POST', body: { email: email.value, password: password.value, nickname: nickname.value } })
    await api('/auth/login', { method: 'POST', body: { email: email.value, password: password.value } })
    await checkSession()
    const redirect = typeof route.query.redirect === 'string' && route.query.redirect.startsWith('/') && !route.query.redirect.startsWith('//') ? route.query.redirect : '/projects'
    await router.replace(redirect)
  } catch (e) { if (e.status === 401) e.message = 'Nieprawidłowy e-mail, hasło lub konto jest czasowo zablokowane.'; cooldown.value = e.retryAfter || 0; error.value = e }
  finally { busy.value = false }
}
</script>
<template><main class="auth-page"><section class="auth-card"><p class="eyebrow">DEVTRACKER</p><h1>{{ registration ? 'Utwórz konto' : 'Wróć do swoich projektów' }}</h1><p class="muted">Projekty, zadania i czas pracy w jednym miejscu.</p><form class="form-stack" @submit.prevent="submit"><ApiFeedback :error="error" /><template v-if="registration"><label for="nickname">Pseudonim</label><Input id="nickname" v-model="nickname" required maxlength="100" autocomplete="nickname" /></template><label for="email">E-mail</label><Input id="email" v-model="email" type="email" required maxlength="256" autocomplete="username" /><label for="password">Hasło</label><div class="toolbar"><Input id="password" v-model="password" :type="visible ? 'text' : 'password'" required :autocomplete="registration ? 'new-password' : 'current-password'" /><Button type="button" variant="outline" :aria-pressed="visible" @click="visible = !visible">{{ visible ? 'Ukryj' : 'Pokaż' }}</Button></div><p v-if="registration" class="muted">Minimum 6 znaków, mała i wielka litera, cyfra i znak specjalny.</p><Button type="submit" :disabled="busy || cooldown > 0">{{ cooldown ? `Spróbuj za ${cooldown} s` : busy ? 'Proszę czekać…' : registration ? 'Zarejestruj i zaloguj' : 'Zaloguj' }}</Button><Button variant="ghost" type="button" @click="registration = !registration; error = null">{{ registration ? 'Mam już konto' : 'Załóż konto' }}</Button></form></section></main></template>
