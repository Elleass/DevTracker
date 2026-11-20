import { defineConfig } from '@playwright/test'
if (!process.env.E2E_CONNECTION) throw new Error('Set E2E_CONNECTION to an isolated PostgreSQL database (never production).')
const apiPort = process.env.E2E_API_PORT || '5034'
const uiPort = process.env.E2E_UI_PORT || '5174'
export default defineConfig({
  testDir: './e2e', workers: 1, timeout: 45000, retries: process.env.CI ? 1 : 0,
  use: { baseURL: `http://localhost:${uiPort}`, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  reporter: [['list'], ['html', { open: 'never' }]],
  webServer: [
    { command: `dotnet run --project ../backend/src/DevTracker.Api --no-build --no-launch-profile -- --migrate=true && dotnet run --project ../backend/src/DevTracker.Api --no-build --launch-profile http -- --urls http://localhost:${apiPort}`,
      url: `http://localhost:${apiPort}/swagger/index.html`, timeout: 120000, reuseExistingServer: false,
      env: { ASPNETCORE_ENVIRONMENT: 'Development', ConnectionStrings__DefaultConnection: process.env.E2E_CONNECTION, Authentication__LoginProtection__PermitLimit: '1000' } },
    { command: `npm run dev -- --host localhost --port ${uiPort} --strictPort`, url: `http://localhost:${uiPort}`, reuseExistingServer: false, env: { API_PROXY_TARGET: `http://localhost:${apiPort}` } },
  ],
})
