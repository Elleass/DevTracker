# DevTracker — frontend

Vue 3, Vite, JavaScript, Vue Router, Tailwind CSS 4 i shadcn-vue (Reka UI).
Komponenty UI są w src/components/ui; PrimeVue usunięto.

```powershell
npm ci
npm run dev
npm test
npm run build
```

API lokalnie: http://localhost:5033; Vite przekazuje /api do backendu.
Cel proxy można nadpisać przez API_PROXY_TARGET. Autoryzacja używa cookies.

Playwright wymaga Chromium i izolowanej bazy PostgreSQL. Konfiguracja, Docker
oraz polecenia testów: [główny README](../README.md).
Architektura i API: [dokumentacja techniczna](../docs/technical.md).
