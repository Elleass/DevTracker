# Scenariusz demonstracji — 3–5 minut

1. **0:00–0:40 Architektura.** Pokazać Api/Application/Domain/Infrastructure,
   Vue, ERD sześciu encji i działające kontenery z osobnym migratorem.
2. **0:40–1:10 Auth.** Zarejestrować konto, zalogować się. Pokazać cookies,
   USER i endpoint administracyjny odrzucający USER statusem 403.
3. **1:10–2:10 Projekty/zadania.** Utworzyć projekt i dwa zadania. Drugiemu
   dodać pierwsze jako poprzednik. Próba zakończenia drugiego daje 409;
   ukończyć pierwsze, potem drugie. Pokazać wyszukiwanie i filtr.
4. **2:10–2:40 Reguła archiwizacji.** Dodać niedokończone zadanie, spróbować
   archiwizacji (409). Ukończyć zadanie, archiwizację pokazać pod koniec.
5. **2:40–3:10 Dokumentacja.** Utworzyć dokument Markdown, zapisać, odświeżyć,
   edytować i pokazać sanitizowany podgląd.
6. **3:10–4:10 Sesje/async.** Rozpocząć pracę, przejść między projektami lub
   odświeżyć stronę — timer pozostaje. Drugi start jest blokowany. Zakończyć,
   pokazać historię i odświeżone statystyki workera.
7. **4:10–5:00 Testy/oddanie.** Pokazać testy, GitHub Actions, Swagger i reguły
   w serwisach; wylogować się i pokazać chroniony deep link. Wskazać konkretny
   commit/tag nagrania.

To instrukcja do nagrania, nie gotowy film. Przed oddaniem wybrać wersję po
zielonym CI i nagrać faktyczny przebieg z tej wersji. Nie tworzyć użytkownika
ADMIN poprzez publiczną rejestrację; użyć udokumentowanej komendy lokalnej.
