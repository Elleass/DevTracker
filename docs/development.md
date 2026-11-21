# Uruchamianie deweloperskie i testy

Szybki start przez Docker Compose znajduje się w [README](../README.md).
Polecenia poniżej wykonuj z katalogu głównego repozytorium, chyba że wskazano `cd frontend`.

## Profil Docker i trwałość danych

Compose jest lokalnym profilem Development. Porty są dostępne na 127.0.0.1.
Wolumen `postgres_data` przechowuje bazę, a `data_protection` klucze cookies.
Przy istniejącym wolumenie użyj dotychczasowego hasła PostgreSQL — zmiana `.env`
nie zmienia hasła w bazie. `docker compose down` zachowuje wolumeny;
`docker compose down -v` je usuwa i nie służy do zwykłego restartu.

Produkcja wymaga HTTPS, konfiguracji zaufanego proxy, własnych sekretów oraz
zaszyfrowania kluczy Data Protection. Lokalne Compose nie zapewnia publicznego
hostingu ani certyfikatów produkcyjnych.

```powershell
docker compose restart api frontend
docker compose logs --tail 100 api migrate
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
