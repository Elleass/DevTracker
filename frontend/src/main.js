
import PrimeVue from 'primevue/config'
import Aura from '@primeuix/themes/aura'
import { createApp } from 'vue'
import App from './App.vue'

import './styles/main.css'
import router from './router/index.js'

const app = createApp(App)

app.use(PrimeVue, {
  theme: {
    preset: Aura
  }
})

app.use(router)
app.mount('#app')