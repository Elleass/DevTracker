# Code review i weryfikacja wymagań

> Dokument historyczny: ocena poprzedza realizację etapów 19–32.
> Aktualny zakres i testy: [raport realizacji](../../docs/implementation-status.md).

Stan na 2026-11-20, branch `project-mvp`. Ocena dotyczy 18 punktów przekazanych
w rozmowie, aktualnego katalogu roboczego oraz wykonanych testów.

**Nie wszystkie wymagania są jeszcze zamknięte.** Funkcje 10–13 działają,
ale pozostały ograniczenia opisane niżej i artefakty formalne.

## Wynik sprawdzeń

- `dotnet test backend/DevTracker.slnx --no-restore` z ustawionym `DEVTRACKER_TEST_CONNECTION`: **54 przechodzące, 0 błędów, 0 pominiętych**.
- W zestawie: 8 testów jednostkowych reguły biznesowej; 34 testy PostgreSQL przez WebApplicationFactory/repozytoria; 12 testów middleware i dokumentacji.
- Kompilacja backendu przy testach: bez błędów i ostrzeżeń.
- `npm run build` w frontend: sukces; ostrzeżenie o paczce JS większej niż 500 kB (około 946 kB przed gzip).
- `git diff --check`: bez błędów białych znaków.
- Testy obejmują prawdziwe migracje, Identity cookies, pełny CRUD, restart hosta,
  walidację, archiwizację, role, paginację i filtry, równoległe zapisy, lockout
  oraz limity logowania. Nie obejmują pełnej demonstracji w frontendzie.
- Testy PostgreSQL używają własnych losowych baz i je usuwają; bez zmiennej
  połączenia są jawnie pomijane, dlatego sam domyślny zielony test run nie
  potwierdza integracji z bazą.

## Ustalenia wymagające poprawek

### Naprawione: współbieżna archiwizacja i ochrona logowania

- Archiwizacja oraz zmiany projektu/zadań korzystają ze wspólnej transakcji
  Read Committed i blokady rekordu projektu `SELECT ... FOR UPDATE`.
  Blokada działa między procesami/instancjami korzystającymi z tej samej bazy.
- Po archiwizacji POST/PATCH Tasks zwracają 409 (`ProjectArchivedException`).
  Usuwanie zadań pozostaje dozwolone. Błędna operacja zwalnia blokadę przez rollback.
- Cztery testy z rzeczywistymi równoległymi żądaniami wymuszają obie kolejności
  archiwizacji i tworzenia/ponownego otwarcia zadania. Oczekiwanie na blokadę
  jest potwierdzane w `pg_stat_activity`, bez polegania na przypadkowym timingu.
- Logowanie zlicza błędne hasła: domyślnie 5 prób blokuje konto na 15 minut.
  Udane logowanie zeruje licznik. Blokada jest zapisywana przez Identity w bazie.
- Polityka IP ogranicza login do 10 żądań na 60 sekund na proces; odrzucenie
  daje 429 ProblemDetails z Retry-After. Nie ufa dowolnemu X-Forwarded-For.
- Sześć testów sprawdza lockout, reset licznika, wygaśnięcie utrwalonej blokady,
  konfigurację, 429 bez wykonania logowania, rozdzielenie IP i odnowienie okna.

### [P2] Hasło lokalnego PostgreSQL jest zapisane w Git

Miejsce: `compose.yaml`, linia 6. Jest to jawne hasło środowiska developerskiego,
nie znaleziony sekret produkcyjny. Jednak literalne kryterium „konfiguracja
bez sekretów w Git” nie jest w ten sposób zamknięte.

Zalecenie: pobierać POSTGRES_PASSWORD z ignorowanego `.env`, dodać `.env.example`
bez rzeczywistego hasła i udokumentować konfigurację istniejącego wolumenu.

### [P3] Lista użytkowników ADMIN pobiera wszystko i wykonuje N+1 zapytań

Miejsce: `src/DevTracker.Api/Controllers/AdminController.cs`, linie 22–27.
Najpierw pobiera wszystkich użytkowników, a potem osobno role każdej osoby.
Przy większej bazie rosną zużycie pamięci, rozmiar odpowiedzi i liczba zapytań.

Zalecenie: paginacja użytkowników oraz zbiorcze pobranie powiązań z rolami.
Tasks nie ma tego problemu: paginacja działa już po stronie PostgreSQL.

### [P3] Formularz logowania próbuje nawigować także po błędzie sieci

Miejsce: `frontend/src/views/LoginView.vue`, linie 39–47. Catch ustawia komunikat,
ale nie kończy funkcji; później wykonywane jest router.replace. Guard sesji
może zatrzymać tę nawigację, ale powstaje zbędna próba przejścia i kolejne żądanie.

Zalecenie: zakończyć funkcję w catch i czyścić poprzedni błąd przy kolejnym submit.

## Stan wszystkich 18 punktów

| Punkt | Ocena | Dowód / brak do zamknięcia |
| --- | --- | --- |
| 01. Audyt REST | Spełnione | Tabela Auth/Projects/Tasks, DTO, statusy w api-audit.md; Swagger UI i testy dokumentacji. |
| 02. Model/PostgreSQL | Spełnione | Trzy aktywne encje, FK, RESTRICT/CASCADE, cztery migracje; test odczytu po restarcie hosta. |
| 03. Projects CRUD | Spełnione | Pięć operacji, 400/404/409, unikalna nazwa per właściciel na POST/PATCH i indeks w bazie. |
| 04. Tasks CRUD | Spełnione | Pięć operacji, poprawny ProjectId, status/termin, nullable PATCH i kontrola przynależności. |
| 05. Walidacja DTO | Spełnione | Pola wymagane, długości, enumy, daty, parametry paginacji; 400 z błędami pól. |
| 06. Globalne wyjątki | Spełnione | IExceptionHandler/ProblemDetails; brak powtarzanych catch w kontrolerach. |
| 07. Archiwizacja | Spełnione | Reguła w serwisie, wspólna blokada PostgreSQL dla zapisów i testy obu kolejności równoległych żądań. |
| 08. Regresja 3.0 | Spełnione | CRUD, statusy, migracje, trwałość oraz współbieżna archiwizacja mają przechodzące testy. |
| 09. Authentication | Spełnione funkcjonalnie | Rejestracja/login/logout/me; testy ważności i wygaśnięcia cookie. Lockout i limitowanie IP są przetestowane. |
| 10. USER/ADMIN | Spełnione | Migracja ról, RoleManager, domyślny USER, lokalny provisioner ADMIN i test braku eskalacji przez rejestrację. |
| 11. Authorization/izolacja | Spełnione | Właściciel projektu i przynależność zadania sprawdzane w serwisach/repozytoriach; ADMIN-only users; testy USER/ADMIN/401/403/404. |
| 12. Paginacja Tasks | Spełnione | pageNumber/pageSize, limit 100, metadane, stabilna kolejność, puste strony i ochrona przed overflow. |
| 13. Filtry/wyszukiwanie | Spełnione | Status i literalny tytuł ILIKE, łączone z paginacją, liczone po filtrach i ograniczone do właściciela/projektu. |
| 14. Dokumentacja API | Częściowo | UI, modele i odpowiedzi są; brak machine-readable cookie securitySchemes/security, a 400 Identity nadal ma inny format niż walidacja DTO. |
| 15. ≥5 unit testów | Spełnione | 8 testów logiki archiwizacji i autoryzacji serwisowej, bez HTTP/bazy. |
| 16. ≥3 integracyjne/konfiguracja | Częściowo | 34 testy PostgreSQL i konfiguracja środowiskowa działają; jawne hasło compose wymaga uporządkowania. Brak CI wymuszającego testy bazy. |
| 17. Dokumentacja projektu | Częściowo | Backend README, uruchomienie, opis API i ERD są; brakuje wspólnego README projektu z architekturą/technologiami. Historię zmian podzielono na tematyczne commity. Frontend README jest szablonem. |
| 18. Oddanie/demonstracja | Niespełnione | Brak oznaczonej wersji do oddania, filmu 3–5 minut i pełnej demonstracji; same commity na branchu nie zamykają tego punktu. |

## Dalsze usprawnienia

- Uniezależnić odpowiedzi Projects od encji domenowych; wrappery odpowiedzi
  nadal zawierają Project, co wiąże publiczny kontrakt z modelem bazy.
- Ujednolicić 400 Identity i DTO do ValidationProblemDetails; opisać cookie
  w securitySchemes oraz security tylko na chronionych operacjach OpenAPI.
- Zastąpić pobieranie całej listy zadań przy archiwizacji zapytaniem EXISTS;
  zachować regułę biznesową w Application.
- Rozważyć indeksy pod ProjectId/Status/CreatedAt/Id i wyszukiwanie substring
  dopiero po analizie rzeczywistych planów zapytań/liczby rekordów.
- Frontend ma logowanie, listę projektów i listę zadań z filtrami/paginacją,
  ale nie ma formularzy pełnego CRUD, archiwizacji, admina ani przycisku logout.
  Backend udostępnia te operacje i można je demonstracyjnie wykonać w Swaggerze.
- Podzielić ładowanie widoków frontendu, aby ograniczyć ostrzeżenie wielkości JS.
- Dopisać CI z PostgreSQL, wykonać pełny scenariusz demonstracji i dopiero wtedy
  oznaczyć wersję do oddania. Zmiany implementacyjne podzielono na osobno testowane commity na project-mvp.

Dokumentacja wspierająca zalecenia:
[EF Core: współbieżność](https://learn.microsoft.com/en-us/ef/core/saving/concurrency),
[Identity: lockout](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0),
[Npgsql: ILIKE](https://www.npgsql.org/efcore/mapping/translations.html).
