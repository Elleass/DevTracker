# Realizacja wymagań na oceny 3.0, 4.0 i 5.0

Stan dokumentacji: **2026-11-21**, gałąź `project-mvp`.
Podstawa: „REGULAMIN ZAJĘĆ”, §4, strony 5–8, semestr zimowy 2026/2027.
Wymagania są kumulatywne: **4.0 wymaga również 3.0, a 5.0 również 3.0 i 4.0**.

Poniższe tabele wskazują sposób realizacji i miejsca w repozytorium, które można
pokazać podczas demonstracji. „Zrealizowane” oznacza istniejącą implementację;
nie jest gwarancją oceny ani potwierdzeniem oddania wymaganych artefaktów.

## Ocena 3.0 — backend, dane i podstawowa logika

| Wymaganie regulaminu | Jak zostało zrealizowane | Dowód w repozytorium |
| --- | --- | --- |
| Działająca aplikacja backendowa i REST API | ASP.NET Core 10; kontrolery Auth, Projects, Tasks, Documents, WorkSessions i Dependencies. | [Kontrolery](../backend/src/DevTracker.Api/Controllers), [Program.cs](../backend/src/DevTracker.Api/Program.cs) |
| Połączenie z bazą danych | EF Core z providerem Npgsql komunikuje się z osobnym serwerem PostgreSQL 18. | [AppDbContext](../backend/src/DevTracker.Infrastructure/Persistence/AppDbContext.cs), [Compose](../compose.yaml) |
| Minimum 3 encje / modele | ApplicationUser, Project i TaskItem; dodatkowo WorkSession, Document i łącznik TaskDependency. | [Encje](../backend/src/DevTracker.Domain/Entities), [ApplicationUser](../backend/src/DevTracker.Infrastructure/Identity/ApplicationUser.cs) |
| Minimum jedna relacja | Użytkownik ma projekty przez OwnerId, projekt ma zadania przez ProjectId. Relacje mają FK w bazie. | [Pełny ERD](technical.md), [Konfiguracje EF](../backend/src/DevTracker.Infrastructure/Persistence/Configuration) |
| CRUD głównych zasobów | Projekty i zadania: tworzenie, lista, szczegóły, edycja i usuwanie. Dokumenty również mają CRUD. | [ProjectsController](../backend/src/DevTracker.Api/Controllers/ProjectsController.cs), [TasksItemController](../backend/src/DevTracker.Api/Controllers/TasksItemController.cs), [CrudIntegrationTests](../backend/tests/DevTracker.Tests/CrudIntegrationTests.cs) |
| Poprawne metody HTTP | GET odczytuje, POST tworzy lub wykonuje akcję, PATCH aktualizuje wybrane pola, PUT zastępuje dane dokumentu, DELETE usuwa. | [Mapa API](technical.md#kontrakty-http) |
| Podział na warstwy | Api obsługuje HTTP, Application przypadki użycia i reguły, Domain model, Infrastructure zapis i mechanizmy techniczne. | [Struktura projektu](../README.md#struktura-projektu), [backend/src](../backend/src) |
| DTO lub analogiczne oddzielenie API od modelu bazy | Osobne request DTO ograniczają dane przyjmowane z klienta. Zadania, dokumenty, sesje i użytkownicy mają DTO odpowiedzi. Odpowiedzi listy/szczegółów projektów opakowują obecnie encję Project — szczegół opisano w ograniczeniach. | [DTO API](../backend/src/DevTracker.Api/DTOs), [WorkspaceResponses](../backend/src/DevTracker.Application/Models/WorkspaceResponses.cs) |
| Walidacja danych wejściowych | DataAnnotations: Required, StringLength, Range i EnumDataType; NotWhiteSpace; IValidatableObject dla parametrów i offsetu strony. ApiController zwraca 400 z błędami pól. | [DTO zadania](../backend/src/DevTracker.Api/DTOs/TaskItems/CreateTaskItemRequest.cs), [TaskListRequest](../backend/src/DevTracker.Api/DTOs/TaskItems/TaskListRequest.cs), [Walidatory](../backend/src/DevTracker.Api/Validation) |
| Globalna obsługa błędów | IExceptionHandler mapuje wyjątki na ProblemDetails, loguje nieoczekiwane błędy i nie ujawnia ich szczegółów klientowi. | [GlobalExceptionHandler](../backend/src/DevTracker.Api/Exceptions/GlobalExceptionHandler.cs), [ExceptionHandlingTests](../backend/tests/DevTracker.Tests/ExceptionHandlingTests.cs) |
| Minimum jedna własna reguła biznesowa | Projektu nie można zarchiwizować, jeśli ma zadanie ze statusem innym niż Done. Serwis odrzuca operację; API zwraca 409. Wspólna blokada projektu zabezpiecza równoległe zmiany. | [ProjectService](../backend/src/DevTracker.Application/Services/ProjectService.cs), [ArchiveProjectTests](../backend/tests/DevTracker.Tests/ArchiveProjectTests.cs), [ProjectConcurrencyTests](../backend/tests/DevTracker.Tests/ProjectConcurrencyTests.cs) |
| Trwałe zapisywanie danych | Zapis do PostgreSQL; wolumen zachowuje bazę po zatrzymaniu kontenerów. Test sprawdza odczyt użytkownika, projektu i zadania po restarcie hosta backendu. | [Compose](../compose.yaml), `Saved_user_project_and_task_survive_backend_host_restart` w [CrudIntegrationTests](../backend/tests/DevTracker.Tests/CrudIntegrationTests.cs) |
| Poprawne kody odpowiedzi | 200/201/204 dla sukcesu; 400 walidacja, 401 brak sesji, 403 brak roli, 404 brak dostępnego zasobu, 409 konflikt reguły, 429 limit, 500 nieoczekiwany błąd. | [Kontrolery](../backend/src/DevTracker.Api/Controllers), [Handler](../backend/src/DevTracker.Api/Exceptions/GlobalExceptionHandler.cs), [Testy API](../backend/tests/DevTracker.Tests) |

**Przykład demonstracji 3.0:** utwórz projekt i zadanie, spróbuj archiwizacji
(409), ukończ zadanie i ponów archiwizację (204). Wyślij też niepoprawne DTO
(400), aby pokazać walidację i opis błędu.

## Ocena 4.0 — bezpieczeństwo, testy i rozwój API

Poniżej wymagania dodatkowe względem 3.0; wszystkie mają implementację.

| Wymaganie regulaminu | Jak zostało zrealizowane | Dowód w repozytorium |
| --- | --- | --- |
| Authentication użytkownika | ASP.NET Core Identity: rejestracja, weryfikacja hasła, cookie DevTracker.Session, GET me i logout. Domyślnie 30 minut sesji bez sliding expiration. | [AuthController](../backend/src/DevTracker.Api/Controllers/AuthController.cs), [Program](../backend/src/DevTracker.Api/Program.cs), [Testy auth](../backend/tests/DevTracker.Tests/AuthAndArchiveIntegrationTests.cs) |
| Authorization | Authorize wymaga sesji; serwisy sprawdzają właściciela projektu. Zadania, dokumenty i sesje są dostępne wyłącznie uprawnionemu użytkownikowi. | [ProjectService](../backend/src/DevTracker.Application/Services/ProjectService.cs), [WorkspaceService](../backend/src/DevTracker.Application/Services/WorkspaceService.cs), [CrudIntegrationTests](../backend/tests/DevTracker.Tests/CrudIntegrationTests.cs) |
| Minimum 2 role | USER i ADMIN zapisane przez migrację. Rejestracja nadaje USER; ADMIN nadaje się zaufaną komendą lokalną, nie polem publicznego formularza. | [ApplicationRoles](../backend/src/DevTracker.Infrastructure/Identity/ApplicationRoles.cs), [AdminRoleProvisioner](../backend/src/DevTracker.Infrastructure/Identity/AdminRoleProvisioner.cs), [Migracje](../backend/src/DevTracker.Infrastructure/Persistence/Migrations) |
| Ograniczenie dostępu do endpointów | GET /api/admin/users wymaga ADMIN. Brak sesji daje 401, USER dostaje 403. ADMIN również nie omija własności projektów. | [AdminController](../backend/src/DevTracker.Api/Controllers/AdminController.cs), [Testy ról](../backend/tests/DevTracker.Tests/AuthAndArchiveIntegrationTests.cs), [Testy izolacji](../backend/tests/DevTracker.Tests/TaskQueryIntegrationTests.cs) |
| Paginacja minimum jednego zasobu | Zadania i historia sesji: pageNumber/pageSize, maksymalnie 100 elementów; odpowiedź zawiera items oraz metadane stron. | [TaskListRequest](../backend/src/DevTracker.Api/DTOs/TaskItems/TaskListRequest.cs), [TaskQueryIntegrationTests](../backend/tests/DevTracker.Tests/TaskQueryIntegrationTests.cs) |
| Wyszukiwanie lub filtrowanie | API zadań filtruje po statusie i wyszukuje po tytule przed paginacją. Vue dodatkowo filtruje pobrane projekty po statusie, archiwizacji i nazwie. | [TaskItemRepository](../backend/src/DevTracker.Infrastructure/Repositories/TaskItemRepository.cs), [ProjectView](../frontend/src/views/ProjectView.vue) |
| Dokumentacja REST API | Swagger UI /swagger i OpenAPI /openapi/v1.json w Development: modele, odpowiedzi HTTP i cookie security. W Production dokumentacja nie jest wystawiona. | [Program](../backend/src/DevTracker.Api/Program.cs), [ApiDocumentationTests](../backend/tests/DevTracker.Tests/ApiDocumentationTests.cs) |
| Minimum 5 unit testów logiki biznesowej | Testy archiwizacji bez HTTP obejmują puste projekty, ukończone/nieukończone zadania, obcego właściciela, brak projektu i ponowienie. Osobne testy sprawdzają graf zależności. | [ArchiveProjectTests](../backend/tests/DevTracker.Tests/ArchiveProjectTests.cs), `DependencyRuleTests` w [WorkspaceTests](../backend/tests/DevTracker.Tests/WorkspaceTests.cs) |
| Minimum 3 integration testy REST API | WebApplicationFactory i rzeczywisty PostgreSQL: CRUD projektów, CRUD zadań, błędne dane, auth, role, izolacja i inne ścieżki HTTP. | [CrudIntegrationTests](../backend/tests/DevTracker.Tests/CrudIntegrationTests.cs), [AuthAndArchiveIntegrationTests](../backend/tests/DevTracker.Tests/AuthAndArchiveIntegrationTests.cs), [PostgreSqlFixture](../backend/tests/DevTracker.Tests/PostgreSqlFixture.cs) |
| Migracje schematu | Sześć migracji EF Core; Database.MigrateAsync stosuje brakujące zmiany. Kontener migrate kończy pracę przed uruchomieniem API. | [Migracje](../backend/src/DevTracker.Infrastructure/Persistence/Migrations), [Program](../backend/src/DevTracker.Api/Program.cs), [Compose](../compose.yaml) |
| Konfiguracja i brak sekretów w repo | Connection string i hasło bazy pochodzą ze zmiennych/ignorowanych plików lokalnych. Repo zawiera wzór .env.example; opcje sesji i ochrony loginu mają walidację konfiguracji. | [.env.example](../.env.example), [.gitignore](../.gitignore), [Konfiguracja API](../backend/src/DevTracker.Api/Configuration), [Instrukcja](development.md) |
| Logika biznesowa oddzielona od HTTP | Reguły znajdują się w serwisach i DependencyRules w Application. Kontrolery przekazują dane i budują odpowiedzi HTTP. | [Serwisy Application](../backend/src/DevTracker.Application/Services) |

**Przykład demonstracji 4.0:** pokaż brak dostępu bez sesji, własne i cudze
projekty, odmowę endpointu ADMIN dla USER oraz listę zadań z filtrem i paginacją.
Uruchom testy jednostkowe i integracyjne.

## Ocena 5.0 — kompletna aplikacja i mechanizmy zaawansowane

Poniżej wymagania dodatkowe względem 4.0; wszystkie mają implementację.

| Wymaganie regulaminu | Jak zostało zrealizowane | Dowód w repozytorium |
| --- | --- | --- |
| Minimum 5 powiązanych encji | ApplicationUser, Project, TaskItem, WorkSession i Document tworzą model biznesowy. TaskDependency jest dodatkowym łącznikiem. Tabele techniczne Identity nie służą do sztucznego zwiększania liczby encji biznesowych. | [ERD](technical.md), [Encje](../backend/src/DevTracker.Domain/Entities), [ApplicationUser](../backend/src/DevTracker.Infrastructure/Identity/ApplicationUser.cs) |
| Minimum 3 różne reguły biznesowe | Jedna aktywna sesja użytkownika; zakaz archiwizacji z otwartymi zadaniami; ukończenie zadania dopiero po poprzednikach. Blokady i częściowy indeks UNIQUE zabezpieczają współbieżność. | [WorkspaceService](../backend/src/DevTracker.Application/Services/WorkspaceService.cs), [ProjectService](../backend/src/DevTracker.Application/Services/ProjectService.cs), [DependencyRules](../backend/src/DevTracker.Application/Services/DependencyRules.cs) |
| Frontend we frameworku | Vue 3, Vue Router, Vite, Tailwind i komponenty shadcn-vue/Reka UI. Interfejs jest po angielsku. | [package.json](../frontend/package.json), [Widoki](../frontend/src/views), [Komponenty](../frontend/src/components) |
| Komunikacja frontendu z REST API | Wspólny klient fetch obsługuje JSON, cookies, odpowiedzi bez treści i błędy. Proxy Vite/nginx przekazuje /api. | [Klient API](../frontend/src/api/client.js), [nginx](../frontend/nginx.conf), [Vite](../frontend/vite.config.js) |
| Logowanie i auth we frontendzie | Formularz logowania/rejestracji, sprawdzanie GET me, guard tras, logout i powrót do logowania po utracie sesji. | [LoginView](../frontend/src/views/LoginView.vue), [Router](../frontend/src/router/index.js), [Stan sesji](../frontend/src/composables/workspace.js) |
| Listowanie, dodawanie, edycja i usuwanie | Projekty, zadania i dokumenty mają formularze CRUD; sesje mają start/stop i historię. Zadania mają priorytety, estymacje i zarządzanie poprzednikami. | [ProjectView](../frontend/src/views/ProjectView.vue), [ProjectForm](../frontend/src/components/ProjectForm.vue), [TaskPanel](../frontend/src/components/TaskPanel.vue), [DocumentPanel](../frontend/src/components/DocumentPanel.vue), [SessionPanel](../frontend/src/components/SessionPanel.vue) |
| Obsługa błędów API we frontendzie | Błędy pól i ProblemDetails, komunikaty 403/404/409/429, przekierowanie po chronionym 401. Błędne logowanie pokazuje prosty komunikat bez traceId. | [ApiFeedback](../frontend/src/components/ApiFeedback.vue), [Klient API](../frontend/src/api/client.js), [LoginView](../frontend/src/views/LoginView.vue) |
| Mechanizm asynchroniczny | Po commicie zakończenia sesji zdarzenie WorkSessionCompleted trafia do Channel. BackgroundService osobno przelicza TotalWorkedSeconds; ponowne przeliczenie nie podwaja wyniku. | [WorkSessionQueue](../backend/src/DevTracker.Infrastructure/Background/WorkSessionQueue.cs), [WorkStatisticsWorker](../backend/src/DevTracker.Infrastructure/Background/WorkStatisticsWorker.cs), [WorkspaceRepository](../backend/src/DevTracker.Infrastructure/Repositories/WorkspaceRepository.cs) |
| Testy najważniejszej logiki | Testy trzech reguł, równoległych operacji, CRUD, auth i workera. Worker jest sprawdzany przy duplikatach zdarzeń, odbudowie projekcji i awarii zapisu. E2E sprawdza główne ścieżki użytkownika. | [ArchiveProjectTests](../backend/tests/DevTracker.Tests/ArchiveProjectTests.cs), [WorkspaceTests](../backend/tests/DevTracker.Tests/WorkspaceTests.cs), [ProjectConcurrencyTests](../backend/tests/DevTracker.Tests/ProjectConcurrencyTests.cs), [Playwright](../frontend/e2e/workspace.spec.js) |
| Docker Compose: osobno backend i baza | Osobne kontenery PostgreSQL, API i frontend nginx oraz jednorazowy migrator. Zależności oczekują na bazę i zakończenie migracji. Wolumeny przechowują bazę i klucze cookies. | [compose.yaml](../compose.yaml), [Backend Dockerfile](../backend/Dockerfile), [Frontend Dockerfile](../frontend/Dockerfile) |
| README pozwalające uruchomić projekt | Opis, funkcje, technologie, diagram architektury, struktura katalogów, konfiguracja .env, polecenia Docker i adresy aplikacji/Swaggera. Szczegóły lokalnego startu i testów są podlinkowane. | [README](../README.md), [Instrukcja deweloperska](development.md) |
| Minimum jeden rzeczywiście używany element rozszerzony | GitHub Actions przy push/PR wykonuje build/test backendu z PostgreSQL, testy/build Vue oraz E2E Chromium; zapisuje raporty jako artefakty. | [Workflow CI](../.github/workflows/ci.yml), [Przebiegi](https://github.com/Elleass/DevTracker/actions) |

**Przykład demonstracji 5.0:** pokaż CRUD we frontendzie, zależności zadań,
start/stop sesji z zachowaniem stanu po odświeżeniu, historię i przeliczone
statystyki. Pokaż także ERD, kontenery oraz rzeczywisty zielony przebieg CI.

## Wymagania formalne — niezależnie od oceny

| Wymaganie | Stan |
| --- | --- |
| Repozytorium GitHub | [Elleass/DevTracker](https://github.com/Elleass/DevTracker), gałąź project-mvp. |
| Historia commitów pokazująca rozwój | Dostępna w Git/GitHub; zmiany funkcji i dokumentacji mają osobne commity. |
| Wskazanie ostatecznej wersji | **Do wskazania przy oddaniu:** konkretny commit albo tag. Tag nie jest obowiązkowy, jeżeli podano commit. |
| README: opis, technologie, uruchomienie i funkcje | [Zrealizowane](../README.md). |
| Diagram modelu danych / ERD | [Zrealizowane](technical.md): fizyczne nazwy, kolumny, PK/FK, typy, NULL, indeksy i ograniczenia. |
| Dokumentacja REST API | Swagger/OpenAPI oraz [opis kontraktów](technical.md#kontrakty-http). |
| Demo video 3–5 minut | **Do dołączenia / potwierdzenia przy oddaniu:** gotowe nagranie nie jest udokumentowane w repozytorium. |
| Wyjaśnienie projektu podczas demonstracji | Trzeba umieć wyjaśnić architekturę, przepływ żądania, model danych, authentication/authorization i przynajmniej jedną regułę biznesową. Materiał: [dokumentacja techniczna](technical.md). |

## Wyniki weryfikacji i granice oceny

Ostatnia pełna regresja z 2026-11-20:

| Zestaw | Wynik |
| --- | --- |
| Backend .NET — łącznie jednostkowe i integracyjne | **70 zaliczonych, 0 błędów, 0 pominiętych**, z rzeczywistym PostgreSQL. |
| Frontend Vitest | **6 zaliczonych**; build produkcyjny Vue zakończony poprawnie. |
| Playwright E2E | **6 zaliczonych** na izolowanej bazie. |
| CI tej wersji implementacji | [Zielony workflow f1db3fa](https://github.com/Elleass/DevTracker/actions/runs/44830468626). |
| Docker Compose | Build, migracje i start API/nginx zweryfikowane; frontend 200, chronione me bez cookie 401. |

Aktualizacja tego raportu zmienia dokumentację; nie jest nowym uruchomieniem
powyższych testów. Polecenia i warunki uruchomienia: [development.md](development.md).
Testy bazodanowe tworzą odrębne bazy `devtracker_tests_*`; bez połączenia testowego
są pomijane. Liczba testów nie jest procentowym pokryciem kodu.

Istotne ograniczenia:

- ProjectListResponse i ProjectDetailsResponse zawierają encję Project. Pełne
  oddzielenie kontraktu odpowiedzi projektów od modelu domenowego jest dalszym
  usprawnieniem; requesty mają osobne DTO.
- Channel jest pamięciowy. Statystyki są spójne z opóźnieniem; worker odbudowuje
  je z utrwalonych sesji przy starcie i okresowo. Nie jest to trwały broker ani
  gwarancja dostarczenia każdego zdarzenia dokładnie raz.
- Compose jest lokalnym profilem HTTP/Development; wdrożenie publiczne wymaga
  konfiguracji HTTPS, proxy i sekretów opisanej w instrukcji.
- Brak procentowego raportu coverage, testów obciążeniowych i pełnego audytu
  dostępności. W poprzednim audycie npm zależności produkcyjne miały 0 podatności,
  a narzędzia deweloperskie 4 zgłoszenia umiarkowane; ten raport nie jest nowym audytem.
- Techniczny zakres jest przygotowany pod 5.0. Regulamin wymaga również
  działającej demonstracji, artefaktów formalnych oraz umiejętności wyjaśnienia
  rozwiązania; spełnienie listy funkcji samo w sobie nie gwarantuje oceny.
