# Plan frontendu — Vue 3 + shadcn-vue

Status: **zrealizowany**, 2026-11-20. Ten dokument zachowuje pierwotny plan
migracji. PrimeVue/Aura zastąpiono shadcn-vue, wdrożono backend i widoki 22–26.
Aktualny zakres oraz wyniki testów zawiera [raport realizacji](implementation-status.md).

## Co już jest i co zachować

| Obszar | Dowód w repo | Plan |
| --- | --- | --- |
| Vue 3/Vite/JavaScript | frontend/package.json, vite.config.js | Rozwijać obecny projekt, bez przebudowy na React. |
| Tailwind CSS 4 | @tailwindcss/vite, styles/main.css | Zachować istniejący build i tokeny kolorów/typografii. |
| PrimeVue/Aura | main.js, package.json | To była dotychczasowa biblioteka; docelowo usunąć po migracji do shadcn-vue. |
| Login | LoginView.vue | Zachować fetch/cookies i komunikaty; zastąpić kontrolki i poprawić ścieżkę błędu sieciowego. |
| Router i guard | router/index.js | Zachować; sprawdzić odświeżenie/deep link, wygasłą sesję i powrót do żądanej trasy. |
| Lista projektów | AppLayout.vue, ProjectsNavigation.vue | Zachować API, dodać odświeżanie po CRUD i stany błędów. |
| Zadania | ProjectOverviewView.vue | Wykorzystać istniejące `items`, filtry i paginację; dodać CRUD, terminy i zależności. |

Brakuje formularzy pełnego CRUD, dokumentów, sesji pracy, przycisku logout,
widoku ADMIN i wspólnego adaptera błędów. Backend WorkSessions/Documents oraz
zależności zadań również wymaga dopisania; UI nie może ich traktować jako gotowe.

## F0. Migracja komponentów do shadcn-vue

Używamy wariantu dla Vue 3. Komponenty trafiają do własnego `src/components/ui`
i można je dostosowywać do istniejącego projektu. Zachować JavaScript;
konfiguracja CLI (`typescript: false`) powinna generować JS zamiast narzucać migrację całej aplikacji
do TypeScript. Przed instalacją sprawdzić wersję CLI pod Tailwind 4 i zapisać
zależności w package-lock.json. Nie uruchamiać ponownie generatora Vite.

Przygotować components.json, aliasy `@/components`, `@/components/ui`, `@/lib`,
helper `cn` oraz zależności wymagane przez wybrane komponenty. Nie kopiować
instrukcji zastępującej cały main.css: świadomie zmapować semantyczne zmienne
shadcn na obecną ciemną paletę i tokeny, bez cyklicznych aliasów CSS.

| Obecnie / potrzeba | Docelowy komponent |
| --- | --- |
| PrimeVue Button | Button |
| InputText, InputGroup | Input + Label/Field, ewentualnie Input Group |
| Password | Input type=password + dostępny przycisk pokaż/ukryj; zachować autocomplete. |
| Message | Alert z wariantem błędu i opisami pól |
| PanelMenu projektów | Sidebar/Collapsible + istniejące RouterLink |
| Formularz create/edit | Dialog lub Sheet; te same komponenty formularza dla create/edit |
| Potwierdzenie usunięcia/archiwizacji | Alert Dialog |
| Zadania i historia | Table + Pagination + Select + Badge |
| Komunikat sukcesu | Sonner; błąd formularza pozostaje również inline |
| Ładowanie/pusty stan | Skeleton/Spinner oraz Empty/tekst z akcją |

Zacząć od Button/Input/Label/Alert/Dialog/AlertDialog, potem Select/Table/
Pagination/Sidebar/Badge/Sonner. Dla obecnych list wystarczy tabela z paginacją
serwerową; TanStack Table dodawać dopiero, gdy potrzebne będą zaawansowane
kolumny/sortowanie. Nie filtrować tylko aktualnej strony w przeglądarce.

Po migracji loginu i nawigacji usunąć `app.use(PrimeVue)`, import Aura,
zależności primevue/@primeuix/themes i nieużywane importy. Weryfikacja: build,
brak importów PrimeVue, działające focus/klawiatura, etykiety i dialogi na
telefonie/desktopie. Nie zmieniać backendu autoryzacji w ramach migracji UI.

## F1. Fundament API, auth i błędów — część punktu 25

Proponowana struktura:

```text
src/
  api/          client.js, auth.js, projects.js, tasks.js, documents.js, workSessions.js
  composables/  useAuth.js, useActiveWorkSession.js, useApiForm.js
  components/
    ui/         komponenty shadcn-vue
    projects/   ProjectForm.vue, ProjectActions.vue
    tasks/      TaskForm.vue, TaskTable.vue, TaskDependencies.vue
    documents/  DocumentForm.vue, DocumentPreview.vue
    sessions/   ActiveSession.vue, WorkSessionHistory.vue
  views/        istniejące widoki + dokumenty, historia sesji i ADMIN
```

`api/client.js`: jeden wrapper fetch, cookies same-origin, JSON, obsługa 204,
AbortSignal oraz normalizacja ProblemDetails, ValidationProblemDetails i
obecnej tablicy IdentityError. API ma dziś wrappery Projects (`projects`,
`project`) i paginowane Tasks (`items`); adaptery muszą respektować kontrakty.

| Status | Zachowanie UI |
| --- | --- |
| 400 | Błędy przy konkretnych polach + podsumowanie; zachować wpisane dane. |
| 401 przy loginie | Komunikat o danych logowania; bez pętli przekierowań. |
| 401 chronionego API | Wyczyścić stan auth, przekierować raz na login i zachować docelową trasę. |
| 403 | Brak uprawnień; nie wylogowywać poprawnie zalogowanej osoby. |
| 404 | Widok/brak zasobu lub komunikat w bieżącym panelu; nie pokazywać cudzych danych. |
| 409 | Pokazać powód biznesowy: archiwizacja, aktywna sesja lub poprzedniki zadania; odświeżyć istotny stan. |
| 429 | Retry-After i czasowe wyłączenie submit loginu; bez automatycznego ponawiania hasła. |
| 500 / sieć | Czytelny błąd i opcja ponowienia odczytu; traceId do diagnozy, bez stack trace. |

`useAuth`: współdzielony stan sprawdzenia sesji, user i role z `/auth/me`.
Nie przechowywać tokenów w localStorage i nie zastępować Identity JWT.
Guard powinien poczekać na pierwsze sprawdzenie sesji. Logout robi POST,
czyści stan i wraca do loginu. Role służą ukrywaniu funkcji; bezpieczeństwo
nadal egzekwuje API. Nie dodawać Pinia tylko dla pojedynczego współdzielonego
stanu — rozważyć je, jeśli liczba niezależnych store rzeczywiście wzrośnie.

Odbiór: testy adaptera błędów, 204, 401 login vs protected endpoint, 403,
przeładowania chronionej trasy, logoutu i błędu sieci bez fałszywej nawigacji.

## F2. Projects — punkt 22

- Lista projektów z pustym stanem i przyciskiem utworzenia.
- Formularz Name/Description, tworzenie i edycja statusu (`projectStatus`).
- Nazwy statusów w UI i jawne mapowanie do istniejącego liczbowego enum API.
- Potwierdzenie usunięcia z opisem kaskady; odświeżenie listy i nawigacji.
- Archiwizacja z obsługą 409; oznaczenie ArchivedAt i blokada niedozwolonych akcji zadań.
- Rozpoznanie aktualizacji częściowej: pominięcie pola nie czyści go, a null
  czyści Description. Formularz nie może przypadkowo resetować statusu.

Odbiór: create→lista→szczegóły→edit→archive/delete po odświeżeniu przeglądarki;
duplikat nazwy, walidacja i cudzy projekt obsłużone.

## F3. Tasks — punkt 23

- Lista z istniejącymi `pageNumber/pageSize/status/search` i metadanymi API.
- Nowy/edytowany tytuł, opis, termin, status; 150/4000 znaków zgodnie z API.
- Daty pokazywać lokalnie, wysyłać ISO z offsetem; czas serwera pozostaje UTC.
- Potwierdzenie usunięcia; po usunięciu ostatniego elementu strony przejść na
  poprzednią poprawną stronę zamiast pozostawiać mylący pusty widok.
- Zależności: wyszukiwany wybór poprzednika z tego samego projektu,
  lista wymaganych zadań i informacja o blokadzie Done.
- Odrzucenie reguły przez API zawsze obsłużyć, nawet jeśli UI wcześniej
  wyłączyło opcję Done. Stan może zmienić inna karta/przeglądarka.
- Abort poprzednich odczytów przy zmianie projektu/filtra; parametry listy
  warto zapisać w query routera, aby refresh i powrót zachowywały widok.

Odbiór: pełny CRUD, zmiana statusu, czyszczenie terminu/opisu, wiele stron,
filtry i brak dostępu do zadania przez inny projectId. Zależności wymagają
ukończenia backendowego kroku 20.

## F4. Documents — punkt 24

Po wdrożeniu backendu dokumentów: lista w projekcie, edytor tytułu i Markdown,
podgląd, zapis oraz potwierdzenie usunięcia. Pierwsza wersja używa Textarea,
bez uploadu/WYSIWYG. Podgląd ma wyłączony surowy HTML albo sanitizację;
nie wolno wkładać niezaufanego Markdown/HTML bezpośrednio do v-html.

Dodać ostrzeżenie o niezapisanych zmianach, stany zapisu i konflikt/błąd
z zachowaniem treści formularza. Odbiór: dokument zachowany po reloadzie,
bezpieczny podgląd, walidacja, CRUD oraz izolacja właścicieli.

## F5. Work Sessions — punkt 26

- Globalny ActiveSession widoczny także po zmianie projektu.
- Start w bieżącym projekcie, stop aktualnej sesji i historia z paginacją.
- Stan pobierany z `/work-sessions/active` po reloadzie i ponownym logowaniu.
- Timer przeglądarki jest prezentacją StartedAt; backend oblicza ostateczny czas.
- Drugi start pokazuje aktywną sesję i akcję jej zakończenia, nie tworzy lokalnego duplikatu.
- Po utracie sesji auth nie zatrzymywać pracy wyłącznie lokalnie: po ponownym
  logowaniu odtworzyć rzeczywisty stan z backendu.
- Statystyki aktualizowane przez worker mogą pojawić się z opóźnieniem;
  po stop odświeżać je ograniczoną liczbę razy lub pokazać stan przeliczania.

Odbiór: start/stop/historia, reload, zmiana trasy, dwie karty, konflikt 409,
wygaśnięcie cookies i odzyskanie aktywnego timera po loginie.

## F6. ADMIN i weryfikacja całości

Dodać prosty widok listy użytkowników z istniejącego `/api/admin/users`,
dostępny w nawigacji dla ADMIN. Nie dodawać formularza samodzielnego nadawania
ADMIN; istniejąca lokalna komenda pozostaje sposobem provisioningu.

Testy: Vitest + Vue Test Utils dla API/composables/formularzy; Playwright dla
scenariuszy przez prawdziwy backend i izolowany PostgreSQL. Sprawdzić także
mobilny layout, tab navigation, focus dialogu, label/error associations,
loading/empty/retry oraz przekierowania 401/403/404.

## Sugerowana kolejność małych PR/commitów

1. shadcn-vue + tokeny + migracja istniejącego loginu/sidebaru; usunięcie PrimeVue.
2. Klient API + adapter błędów + uporządkowany wspólny stan auth/logout.
3. Projects CRUD i archiwizacja.
4. Tasks CRUD; osobno zależności po ukończeniu backendu grafu.
5. Documents CRUD i bezpieczny podgląd.
6. Work Sessions UI i historia.
7. ADMIN, regresja E2E i poprawki dostępności.

## Dokumentacja komponentów

- [Instalacja w istniejącym Vite](https://www.shadcn-vue.com/docs/installation/vite)
- [components.json](https://www.shadcn-vue.com/docs/components-json)
- [Katalog komponentów](https://www.shadcn-vue.com/docs/components)
- [JavaScript](https://www.shadcn-vue.com/docs/javascript)
