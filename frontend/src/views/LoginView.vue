<script setup>
import { Button, InputGroupAddon, InputText, Password } from 'primevue';
import InputGroup from 'primevue/inputgroup';
import Message from 'primevue/message'
import { ref } from 'vue'
import { useRouter } from 'vue-router';
import { errorMessages } from 'vue/compiler-sfc';

const email = ref('')
const password = ref('')
const errorMessage = ref('')
const router = useRouter();
const isLoading = ref(false)
async function submitForm() {
    if (isLoading.value) return
    isLoading.value = true
    const loginData = {
        email: email.value,
        password: password.value
    }
    const requestOptions = {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({

            "Email": loginData.email,
            "Password": loginData.password
        })
    }
    try {
        const loginResponse = await fetch('/api/auth/login', requestOptions)
        if (!loginResponse.ok) {
            errorMessage.value = loginResponse.status === 401
                ? 'Nieprawidłowy e-mail lub hasło'
                : 'Nie udało się zalogować'
            return
        }

    } catch {
        errorMessage.value = 'Brak połączenia z serwerem'

    } finally {
        isLoading.value = false;
    }


    await router.replace({ name: 'projects' })

}

errorMessage.value = ''


</script>

<template>
    <form @submit.prevent="submitForm" class='flex flex-col gap-4'>
        <label for="email">E-mail</label>
        <InputGroup>
            <InputGroupAddon>@</InputGroupAddon>
            <InputText id="email" type="email" v-model="email" required />
        </InputGroup>

        <label for="password">Hasło</label>
        <Password inputId="password" v-model="password" :feedback="false" toggleMask required />

        <Button type="submit" label="Zaloguj" :loading="isLoading" :disabled="isLoading" />
    </form>
    <Message v-if="errorMessage" severity="error">
        {{ errorMessage }}
    </Message>

</template>