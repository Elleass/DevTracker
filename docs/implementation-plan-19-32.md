# Plan realizacji etapów 19–32

Status: **zrealizowany**, 2026-11-20. Poniżej zachowano pierwotny plan i stan
wyjściowy. Aktualną implementację, wyniki testów i różnice względem planu opisuje
[raport realizacji](implementation-status.md). Prace 01–18 opisuje
[wcześniejszy code review](../backend/docs/code-review.md).

## Punkt wyjścia i decyzje

Backend ma Identity cookies, USER/ADMIN, Projects/Tasks CRUD, walidację,
ProblemDetails, Swagger, paginację/filtry oraz archiwizację zabezpieczoną
blokadą PostgreSQL. Obecna regresja: 54 testy bez błędów i pominięć.

Frontend ma Vue 3, Vite, JavaScript, Vue Router, Tailwind CSS 4 i obecnie
PrimeVue/Aura. Są login, guard, nawigacja projektów oraz lista zadań z filtrami
i stronicowaniem. WorkSession, Document i zależności zadań jeszcze nie istnieją.

Docelowe komponenty UI: **shadcn-vue**, zgodnie z decyzją użytkownika; szczegółowy
[plan frontendu i migracji PrimeVue](frontend-plan.md). Zachowujemy Vue,
cookies, routing i obecną paletę/typografię. Nie planujemy przejścia na React.

Kolejność wykonania: **19 → 20/21 → fundament UI/25 → 22/23 → 24/26 → 27 → 28 → 29 → 30 → 31/32**.
Testy, migracje i opisy API powstają przy każdym kroku, a etap 28 zamyka ich
pełną regresję. Workflow CI warto uruchomić wcześniej, po pierwszym stabilnym
zestawie testów frontendu, a następnie rozszerzać.

## Etap I — model i trzy reguły

### 19. Minimum pięć powiązanych encji

| Encja | Nowe pola i relacje |
| --- | --- |
| User | Istniejący ApplicationUser; właściciel projektów i sesji pracy. |
| Project | Istniejący projekt; kolekcje zadań, dokumentów i sesji. |
| TaskItem | Istniejące zadanie; relacja do wymaganych poprzedników. |
| WorkSession | Id, UserId FK, ProjectId FK, StartedAt UTC, EndedAt nullable; stan aktywny wynika z EndedAt=null. |
| Document | Id, ProjectId FK, CreatedByUserId FK, Title, ContentMarkdown, CreatedAt/UpdatedAt UTC. |
| TaskDependency | Techniczna encja łącząca TaskId i PredecessorTaskId; klucz złożony zapobiega duplikatom. |

Relacje: User→Projects→Tasks/Documents/WorkSessions; WorkSession ma dodatkowo
UserId. TaskDependency łączy dwa zadania tego samego projektu.

Proponowana polityka usuwania: zachować obecne twarde usuwanie projektu i
kaskadowo usuwać należące do niego dokumenty, sesje i zależności. UI musi
wyraźnie informować o usuwaniu historii; archiwizacja służy jej zachowaniu.
Usuwanie użytkownika z projektami/sesjami pozostaje ograniczone przez FK.
Usuwanie pojedynczego zadania będącego poprzednikiem wymaga najpierw usunięcia
zależności; nie usuwamy ich po cichu.

Migracje rozdzielić na WorkSessions, Documents i TaskDependencies. Dodać indeksy
sesji po użytkowniku/projekcie/czasie oraz **unikalny indeks częściowy na UserId
WHERE EndedAt IS NULL**. Tylko sprawdzenie w serwisie nie zabezpieczy dwóch
równoległych startów w różnych projektach.

Odbiór: migracje od pustej bazy i z obecnej wersji; brak osieroconych rekordów;
testy FK, kaskad i indeksu aktywnej sesji. Encje nie są zwracane bezpośrednio
przez nowe endpointy — używamy DTO.

### 20. Minimum trzy niezależne reguły biznesowe

| Reguła | Miejsce i sposób egzekwowania | Kryterium/test |
| --- | --- | --- |
| Jedna aktywna sesja pracy na użytkownika | WorkSessionService + unikalny częściowy indeks bazy; konflikt tłumaczony na wyjątek aplikacyjny/409. | Drugi start, także równoległy w innym projekcie, daje 409; start po zakończeniu jest możliwy. |
| Zakaz archiwizacji z niedokończonymi zadaniami | Istniejący ProjectService i wspólna blokada projektu. | Zachować aktualne unit/integration/concurrency tests; rozszerzenia nie obchodzą blokady. |
| Done dopiero po ukończeniu wymaganych poprzedników | TaskItemService przy zmianie statusu, z blokadą projektu obejmującą również zmiany zależności. | Nieukończony poprzednik daje 409; wszystkie Done pozwalają zakończyć zadanie. |

Dla zależności odrzucać krawędź do samego siebie, do innego projektu oraz
tworzącą cykl. Równoległe dodawanie zależności musi korzystać z istniejącej
blokady projektu. Cofnięcie Done poprzednika mającego zakończone zadania
zależne odrzucać jako 409, aby nie zepsuć już spełnionego warunku.

Nowe reguły pozostają w Application/Domain. Infrastructure zapewnia transakcje,
indeksy i mapowanie konfliktów DB. Api dodaje jawne przypadki wyjątków do
GlobalExceptionHandler oraz opisy 400/401/404/409 w OpenAPI.

### 21. Work Sessions

Proponowany kontrakt:

| Metoda | Endpoint | Rezultat |
| --- | --- | --- |
| POST | `/api/projects/{projectId}/work-sessions` | 201 + Location; start według czasu serwera. |
| GET | `/api/work-sessions/active` | 200 z aktywną sesją lub null dla bieżącego użytkownika. |
| GET | `/api/work-sessions/{sessionId:guid}` | 200 DTO lub 404. |
| POST | `/api/work-sessions/{sessionId}/stop` | 200 zakończonej sesji; ponowienie zachowuje pierwotne EndedAt. |
| GET | `/api/work-sessions?projectId=...&pageNumber=1&pageSize=20` | Stronicowana historia własnych sesji; opcjonalny filtr projektu. |

DTO zawiera Id, ProjectId, StartedAt, EndedAt, DurationSeconds i stan aktywności.
UserId i czas rozpoczęcia/zakończenia ustala serwer. Zakończenie nie przyjmuje
czasu obliczonego przez przeglądarkę. Wartości Duration nie mogą być ujemne.

Start w zarchiwizowanym projekcie jest odrzucany; korzysta z blokady projektu,
więc równoległa archiwizacja nie omija sprawdzenia. Zakończenie własnej aktywnej
sesji pozostaje możliwe także po archiwizacji projektu. Cudza sesja/projekt
daje 404. Historia jest sortowana StartedAt DESC, Id DESC.

Odbiór: start→aktywna sesja→stop→historia; ponowny stop; restart backendu;
izolacja użytkowników; równoległy start i idempotentne zakończenie.

## Etap II — kompletny frontend

Realizacja i komponenty są rozpisane w [frontend-plan.md](frontend-plan.md).

| Nr | Zakres | Warunek odbioru |
| --- | --- | --- |
| 22 | Projects CRUD: lista, szczegóły, formularz tworzenia/edycji, potwierdzenie usunięcia i archiwizacja. | Zmiany widoczne po odświeżeniu; 400/404/409 pokazane; nawigacja aktualizuje się po zmianie projektu. |
| 23 | Tasks CRUD, status, termin, paginacja/wyszukiwanie oraz wymagane poprzedniki. | Pełny scenariusz zadania i odrzucenie Done z niedokończonym poprzednikiem; brak utraty ProjectId. |
| 24 | Dokumenty: lista, szczegóły, tworzenie/edycja/usuwanie Markdown. Backend: pięć operacji `/api/projects/{id}/documents`. | Po przeładowaniu treść jest zachowana; cudzy projekt niedostępny; podgląd nie wykonuje HTML/skryptów. |
| 25 | Wspólny klient API, błędy pól, komunikaty operacji, obsługa 401/403/404/409/429/500. | Przekierowanie po wygaśnięciu sesji, czytelny konflikt reguły, Retry-After dla loginu i zachowanie wpisanych danych po błędzie formularza. |
| 26 | Globalny licznik aktywnej sesji, start/stop w projekcie, stronicowana historia. | Odtworzenie aktywnej sesji po reloadzie, blokada drugiego startu, prawidłowy czas po ponownym logowaniu i zmiana projektu bez utraty timera. |

Dokumenty w pierwszej wersji są tekstem Markdown, bez uploadu plików i bez
zależności od edytora WYSIWYG. Nie zakładamy, że backend dokumentów już istnieje.

## Etap III — mechanizmy zaawansowane

### 27. BackgroundService + Channel<T>

Zakończenie sesji zapisuje EndedAt, a po zatwierdzeniu transakcji publikuje
`WorkSessionCompleted(SessionId, ProjectId, UserId, EventId)` do ograniczonego
Channel. Hosted BackgroundService pobiera zdarzenia i tworzy osobny DI scope
oraz DbContext dla przetwarzania.

Efekt: odświeżenie projekcji czasu pracy projektu, np. TotalWorkedSeconds,
na podstawie zakończonych WorkSessions. **Przeliczenie z danych źródłowych zamiast
ślepego dodawania czasu** zapewnia idempotencję przy ponownym zdarzeniu.

Channel jest pamięciowy: sam nie gwarantuje zachowania zdarzeń po awarii.
Dlatego plan obejmuje odtwarzanie projekcji przy starcie i okresową rekonsyliację
z zakończonych sesji. Utrata zdarzenia pomiędzy commitem a kolejką nie może
trwale zaniżyć statystyk. Jeśli potrzebna będzie gwarantowana dostawa każdego
zdarzenia, kolejnym krokiem jest trwały outbox w tej samej transakcji.

Ustalić pojemność kolejki, ograniczone ponowienia, logowanie błędów i zachowanie
przy zatrzymaniu aplikacji. Pełna kolejka nie cofa poprawnie zakończonej sesji;
brakujące przeliczenie przejmie rekonsyliacja. Statystyki są jawnie eventually
consistent; sesje stanowią źródło prawdy.

Odbiór: zakończona sesja aktualizuje statystyki; duplikat nie podwaja czasu;
błąd/restart nie gubi trwałych danych; ponowne uruchomienie naprawia projekcję.

### 28. Testy

- Unit: trzy reguły, różne statusy poprzedników, self-dependency/cykle,
  odwrócenie Done, obliczenie czasu i idempotencja statystyk.
- PostgreSQL/WebApplicationFactory: nowe CRUD, właściciel, FK/migracje,
  start/stop/historia, indeks aktywnej sesji, równoległe starty i zależności.
- Worker: odbiór, nowy scope, wyjątek/ponowienie, duplikat, shutdown i recovery.
- Frontend: walidacja, adapter błędów, session guard, dialogi i stan aktywnej sesji.
- Browser E2E: login→projekt→zadania/zależności→dokument→sesja pracy→logout;
  oddzielny scenariusz dwóch użytkowników i wygaśnięcia cookies.

Odbiór: testy API z PostgreSQL nie są pomijane w CI; osobno raportowane unit,
integracyjne i E2E. Utrzymać obecną regresję 54 testów.

### 29. GitHub Actions

Workflow dla push i pull_request, z jobami:

1. Backend: .NET 10, restore/build/test, PostgreSQL service, testowa konfiguracja
   połączenia oraz raport wyników. Zmienna DEVTRACKER_TEST_CONNECTION musi istnieć.
2. Frontend: Node zgodny z engines, `npm ci`, build oraz Vitest.
3. E2E po buildzie: backend + izolowana baza + frontend, instalacja przeglądarki
   Playwright i artefakty trace/screenshot przy błędzie.

Cache zależności, minimalne uprawnienia workflow, brak produkcyjnych sekretów
w logach i brak dostępu do nich w testach forków. Odbiór: celowo uszkodzony test
blokuje zielony wynik; czysty checkout przechodzi wszystkie joby.

## Etap IV — uruchamianie i oddanie

### 30. Docker Compose

Dodać Dockerfile API z oddzielnym build/runtime stage oraz serwisy PostgreSQL
i API. Frontend jako trzeci kontener z reverse proxy `/api` jest docelowym
wariantem pełnej demonstracji. PostgreSQL ma healthcheck i trwały wolumen.
Migracje uruchamia osobny krok/kontener przed API, z czytelnym błędem przy awarii.

Hasła przenieść do ignorowanego `.env`, dodać bezsekretowy `.env.example`;
instrukcja musi uwzględnić już istniejący wolumen. Nie używać `down -v` jako
zwykłego sposobu restartu. Zaplanować trwałe klucze Data Protection dla cookies.
Przy reverse proxy skonfigurować wyłącznie zaufane proxy i HTTPS, aby nie
osłabić obecnych Secure cookies i limitowania po IP.

Lokalny profil demonstracyjny powinien działać w Development przez HTTP,
z portami hosta ograniczonymi do 127.0.0.1. Profil produkcyjny wymaga HTTPS
na reverse proxy i poprawnego ForwardedProto od zaufanego proxy; inaczej
Secure cookies nie będą działać w zwykłej przeglądarce przez HTTP.

Odbiór: czysty `docker compose up --build` uruchamia aplikację, migracje są
zastosowane, frontend komunikuje się z API, restart zachowuje zapisane dane.

### 31. README

Wspólny README w root: cel projektu, funkcje, technologie i wersje, struktura
Api/Application/Domain/Infrastructure/frontend, lokalny start i Compose,
konfiguracja, migracje, role/nadanie ADMIN, testy i troubleshooting.
Linki do planu, ERD, Swaggera i opisu reguł. Instrukcję sprawdzić na czystym
checkout bez lokalnych ignorowanych plików autora.

### 32. Dokumentacja techniczna

Zaktualizować ERD do 5+ encji, mapę endpointów/DTO/statusów, cookie security
w OpenAPI i schemat worker/kolejka/projekcja. Opisać trzy reguły wraz z
mechanizmami współbieżności, własnością danych i odzyskiwaniem statystyk.

Na końcu: pełny scenariusz demonstracji, wersja/tag po zielonym CI, nagranie
3–5 minut oraz pokazanie architektury, auth i reguł. Sam push na branch nie
zastępuje oznaczenia wersji do oddania.

## Proponowane kolejne atomowe zmiany

1. WorkSession + migracja + reguła pojedynczej aktywnej sesji i testy.
2. Document + migracja + bezpieczny CRUD i testy.
3. TaskDependency + walidacja grafu + reguła Done i testy współbieżności.
4. Fundament shadcn-vue i migracja istniejących kontrolek (bez nowych funkcji).
5. Wspólny klient API/auth/error UX, potem osobno Projects, Tasks, Documents i Sessions UI.
6. Worker Channel/BackgroundService + statystyki/recovery + testy.
7. CI, pełny Compose i dokumentacja — oddzielne commity.

## Źródła decyzji technicznych

- [shadcn-vue dla Vite](https://www.shadcn-vue.com/docs/installation/vite)
- [Konfiguracja shadcn-vue](https://www.shadcn-vue.com/docs/components-json)
- [Komponenty shadcn-vue](https://www.shadcn-vue.com/docs/components)
- [PostgreSQL: blokady](https://www.postgresql.org/docs/18/explicit-locking.html)
