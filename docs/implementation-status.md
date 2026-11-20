# Realizacja etapów 19–32

Stan: 2026-11-20, gałąź `project-mvp`. Funkcje poniżej są zaimplementowane;
pozostałe plany zachowują historyczny stan wyjściowy.

| Etap | Wynik i dowód |
| --- | --- |
| 19. Powiązane encje | User, Project, TaskItem, WorkSession, Document i łącznik TaskDependency. Migracja `20261120212219_AddWorkspaceModel`: tabele, FK, indeksy i ograniczenia. |
| 20. Trzy reguły | Jedna aktywna sesja użytkownika; zakaz archiwizacji z niezakończonymi zadaniami; ukończenie zadania wymaga ukończenia poprzedników. Warstwa aplikacyjna i ochrona współbieżności. |
| 21. Work Sessions | Start, idempotentny stop, aktywna sesja i stronicowana historia. Częściowy indeks UNIQUE chroni równoległy start w różnych projektach. |
| 22. Projects w Vue | Lista, tworzenie, szczegóły, edycja, archiwizacja i usuwanie. Odświeżana nawigacja i potwierdzenia operacji. |
| 23. Tasks w Vue | CRUD, status, termin, filtry, wyszukiwanie, paginacja i poprzednicy. Błędy 409 prezentują przyczynę odrzucenia. |
| 24. Documentation | REST i Vue CRUD dokumentów projektu; Markdown z sanitizowanym podglądem. Potwierdzenie opuszczenia niezapisanej edycji. |
| 25. Błędy API | Wspólny klient: ValidationProblemDetails, ProblemDetails, IdentityError, 401/403/404/409/429 i błędy sieci. Przekierowanie po utracie sesji, trasa powrotna, zachowanie danych formularza. |
| 26. Sesje w Vue | Start w projekcie, globalny licznik i stop, historia globalna i projektowa. Stan odtwarzany z API po odświeżeniu strony. |
| 27. Mechanizm asynchroniczny | WorkSessionCompleted → ograniczony Channel → BackgroundService → Project.TotalWorkedSeconds. Utrwalone sesje są źródłem prawdy; restart i okresowe uzgadnianie odzyskują utracone sygnały. |
| 28. Testy | 69 .NET, 6 Vitest, 4 Playwright. PostgreSQL: równoległe starty, reguły zależności, izolacja, dokumenty, duplikaty zdarzeń, restart projekcji, awaria i odzyskanie przetwarzania. |
| 29. GitHub Actions | `.github/workflows/ci.yml`: build/test backendu z PostgreSQL 18, testy/build Vue i E2E Chromium przy push/PR. Raporty jako artefakty. |
| 30. Docker Compose | PostgreSQL, jednorazowy migrator, ASP.NET Core i nginx/Vue. Trwałe wolumeny bazy i kluczy cookies; oczekiwanie na bazę i migracje. |
| 31. README | [README](../README.md): stos, funkcje, Docker, lokalny start, zmienne, migracje, ADMIN i testy. |
| 32. Dokumentacja | [Architektura i ERD](technical.md), Swagger/OpenAPI z cookie auth i odpowiedziami HTTP oraz [scenariusz demonstracji](demo.md). |

## Weryfikacja lokalna

- `dotnet test backend/DevTracker.slnx --no-restore` z PostgreSQL:
  **69 zaliczonych, 0 błędów, 0 pominiętych**.
- `npm test`: **6 zaliczonych**; `npm run build`: sukces.
- `npm run test:e2e` z odrębną bazą: **4 zaliczone**. Pełna ścieżka obejmuje
  logowanie, projekty, zadania, odrzuconą archiwizację, dokumenty (także XSS),
  sesję z odświeżeniem strony, edycję/usuwanie i logout. Osobne scenariusze:
  deep link, utrata cookie, USER bez dostępu do cudzych danych i ADMIN.
- `docker compose up -d --build`: migracje zastosowane, API i frontend działają.
  nginx obsługuje SPA i `/api`; niezalogowane `/api/auth/me` daje 401.
- `npm audit --omit=dev`: 0 podatności zależności produkcyjnych.

Wynik lokalny nie deklaruje wyniku zdalnego workflow. Przebiegi:
[GitHub Actions](https://github.com/Elleass/DevTracker/actions).

## Decyzje i ograniczenia

- shadcn-vue zastępuje PrimeVue/Aura; pozostają Vue 3 i JavaScript. Komponenty
  wygenerowano oficjalnym CLI do `frontend/src/components/ui`.
- Nowy model wdrożono jedną migracją zamiast trzech proponowanych w planie.
  Dotychczasowe migracje i dane pozostają zachowane.
- Kanał jest w pamięci procesu. Statystyka może być opóźniona; worker po starcie
  i co około 30 sekund przelicza ją z zapisanych sesji. Nie jest to trwały
  transport dowolnych zdarzeń ani dostarczanie exactly-once.
- Zależności chronią także przed cyklem, samoodwołaniem, cudzym projektem,
  cofnięciem statusu wymaganego poprzednika i jego usunięciem.
- Lokalny Compose używa HTTP i Development dla Swaggera. Produkcja wymaga
  własnych sekretów, HTTPS i konfiguracji opisanej w README.
- Pełny audit npm wykazuje 4 umiarkowane zgłoszenia w narzędziach deweloperskich.
  Nie wymuszano zmian łamiących zgodność; zależności produkcyjne mają czysty audit.
- `docs/demo.md` to scenariusz, nie nagrany film. Nie utworzono nagrania ani
  formalnego tagu wydania z wcześniejszego etapu 18.
