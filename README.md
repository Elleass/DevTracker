# DevTracker

Aplikacja do zarządzania projektami programistycznymi: zadania i ich zależności,
dokumentacja Markdown oraz pomiar czasu pracy. Vue 3 komunikuje się z REST API
ASP.NET Core przez cookies Identity. Dane są przechowywane w PostgreSQL.

## Funkcje

- Rejestracja, logowanie, logout, sesje 30 minut, role USER/ADMIN.
- Projects CRUD i archiwizacja; Tasks CRUD, terminy, paginacja i filtry.
- Graf poprzedników: zakaz cykli i zakończenia zadania przed poprzednikami.
- Dokumenty projektu: CRUD, Markdown z podglądem sanitizowanym przez DOMPurify.
- Jedna aktywna sesja pracy na użytkownika; start/stop/historia i timer w UI.
- Statystyki czasu pracy przeliczane w tle przez BackgroundService/Channel.
- Izolacja danych użytkowników, walidacja DTO i ProblemDetails w API.
- Ochrona loginu: 5 błędów → 15 minut blokady konta; limit 10 żądań/min/IP.

## Technologie i architektura

.NET SDK/ASP.NET Core 10, EF Core 10/Npgsql, PostgreSQL 18; Vue 3, JavaScript,
Vite 8, Tailwind 4, shadcn-vue/Reka UI. Testy: xUnit/WebApplicationFactory,
Vitest/Vue Test Utils i Playwright. PrimeVue został usunięty.

```text
backend/src/DevTracker.Domain          encje i statusy
backend/src/DevTracker.Application     reguły, serwisy, interfejsy, DTO odpowiedzi
backend/src/DevTracker.Infrastructure  PostgreSQL, repozytoria, Identity, worker
backend/src/DevTracker.Api             HTTP, DTO wejściowe, auth, OpenAPI
backend/tests/DevTracker.Tests         unit/integration/concurrency tests
frontend/src                          widoki Vue, API client i shadcn-vue
docs/technical.md                     ERD, kontrakty, reguły, mechanizm asynchroniczny
```

Application zależy od Domain; Infrastructure implementuje interfejsy Application.
Api rejestruje zależności i wystawia HTTP. Reguły nie znajdują się w kontrolerach.

## Szybkie uruchomienie — Docker Compose

Wymagany Docker Desktop/Engine z Compose. Z katalogu głównego:

```powershell
Copy-Item .env.example .env
# Ustaw POSTGRES_PASSWORD w .env na własne hasło.
docker compose up --build -d
docker compose ps
```

- Aplikacja: [localhost:8088](http://localhost:8088)
- Swagger UI: [localhost:5033/swagger](http://localhost:5033/swagger)
- OpenAPI: [localhost:5033/openapi/v1.json](http://localhost:5033/openapi/v1.json)

Najpierw startuje zdrowa baza, potem jednorazowy kontener `migrate` stosuje
migracje; dopiero po sukcesie uruchamiają się API i frontend. Zakończony migrator
z kodem 0 jest prawidłowym stanem. API działa jako użytkownik `app`.

Compose jest **lokalnym profilem Development**, z portami dostępnymi wyłącznie
na 127.0.0.1. Produkcyjne wystawienie wymaga HTTPS, konfiguracji zaufanego proxy,
sekretów i zaszyfrowania kluczy Data Protection. W tym profilu nie ma publicznego
hostingu ani certyfikatów produkcyjnych.

Wolumen `postgres_data` zachowuje dane, `data_protection` zachowuje klucze cookies.
Jeśli używasz istniejącego wolumenu, podaj jego dotychczasowe hasło: zmiana `.env`
nie zmienia hasła istniejącego PostgreSQL. Nie używaj `down -v` do zwykłego restartu.

```powershell
docker compose restart api frontend
docker compose logs --tail 100 api migrate
docker compose down
```

## Lokalne uruchomienie bez kontenerów aplikacji

Wymagania: .NET SDK 10, Node 24.12+ (lub zgodny z frontend/package.json), npm,
PostgreSQL 18, narzędzie `dotnet-ef` w wersji 10.0.13.

```powershell
docker compose up -d db
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Database=devtracker;Username=postgres;Password=<lokalne-haslo>'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project backend/src/DevTracker.Api -- --migrate=true
dotnet run --project backend/src/DevTracker.Api --launch-profile http
```

W drugim terminalu:

```powershell
cd frontend
npm ci
npm run dev
```

Frontend Vite podaje adres lokalny i przekazuje `/api` do portu 5033. Nie uruchamiaj
lokalnego API i kontenerowego API jednocześnie na tym samym porcie.

## Konfiguracja i role

Connection string podaj przez `ConnectionStrings__DefaultConnection` lub
ignorowany `backend/src/DevTracker.Api/appsettings.Development.json`. Nie commituj
`.env`, haseł ani kluczy. `.env.example` zawiera wyłącznie wzór konfiguracji.

Rejestracja przydziela USER. ADMIN przyznaje się istniejącemu kontu lokalnie:

```powershell
dotnet run --project backend/src/DevTracker.Api -- --grant-admin email@example.com
# Albo dla Compose:
docker compose exec api dotnet DevTracker.Api.dll --grant-admin email@example.com
```

Po nadaniu roli zaloguj się ponownie. ADMIN ma `/api/admin/users`, ale nie omija
kontroli właściciela projektów. Hasła kont nie są ustalane przez seeder.
Sesja jest cookie HttpOnly/SameSite=Lax; poza Development także Secure.

Opcje środowiskowe: `Authentication__Session__LifetimeMinutes` (1–1440),
`Authentication__LoginProtection__MaxFailedAttempts`, `LockoutMinutes`,
`PermitLimit`, `WindowSeconds`; `BackgroundProcessing__Enabled=false` wyłącza
worker, `DataProtection__KeysPath` określa katalog kluczy dla kontenerów.
Limit IP jest lokalny dla procesu; wiele replik wymaga wspólnego limitowania
na brzegu infrastruktury. Nagłówki forwarding nie są automatycznie zaufane.

## Build i testy

```powershell
dotnet build backend/DevTracker.slnx
$env:DEVTRACKER_TEST_CONNECTION = 'Host=localhost;Database=postgres;Username=postgres;Password=<testowe-haslo>'
dotnet test backend/DevTracker.slnx
cd frontend
npm ci
npm test
npm run build
```

Testy backendu potrzebują uprawnień CREATE DATABASE: tworzą i usuwają wyłącznie
losowe `devtracker_tests_*`. Bez zmiennej połączenia integracyjne testy PostgreSQL
są pomijane; nie traktuj takiego wyniku jako pełnej weryfikacji.

Testy przeglądarkowe uruchamiają własne API/Vite. Przygotuj **oddzielną pustą
bazę** E2E, nigdy bazę użytkowników/produkcji, i zwolnij porty 5034/5174
(można nadpisać przez E2E_API_PORT/E2E_UI_PORT):

```powershell
cd frontend
npx playwright install chromium
$env:E2E_CONNECTION = 'Host=localhost;Database=devtracker_e2e;Username=postgres;Password=<testowe-haslo>'
npm run test:e2e
```

Migracje E2E stosowane są automatycznie. Scenariusze tworzą własne konta i dane
w tej testowej bazie; bazę można potem usunąć. Raport: frontend/playwright-report.
GitHub Actions uruchamia build/test backendu z PostgreSQL, testy/build frontendu
oraz E2E; raporty testów są artefaktami workflow. Hasło `ci-only-password` dotyczy
wyłącznie jednorazowej bazy service joba i nie jest sekretem środowiska aplikacji.

## Dokumentacja

- [Architektura, ERD, reguły i API](docs/technical.md)
- [Scenariusz demonstracji](docs/demo.md)
- [Wyniki etapów 19–32](docs/implementation-status.md)
- [Pierwotny plan](docs/implementation-plan-19-32.md) i [plan UI](docs/frontend-plan.md)
- Historia: `git log --oneline`; aktualna gałąź robocza: `project-mvp`.
