---
name: rocketmq-engineering-thesis
description: Tworzy i redaguje opartą na dowodach pracę inżynierską w języku polskim pt. „Projekt i implementacja systemu kolejkowania wiadomości” na podstawie repozytorium RocketMQ. Używaj przy planowaniu struktury pracy, formułowaniu celu i wymagań, pisaniu rozdziałów, opisie architektury i implementacji, przygotowywaniu diagramów, analizie testów i benchmarków, budowaniu bibliografii, korekcie językowej oraz audycie spójności tez z kodem, testami, ADR-ami i dokumentacją projektu. Nie utożsamiaj tego projektu automatycznie z Apache RocketMQ.
---

# Praca inżynierska RocketMQ

## Zachowaj rygor akademicki

- Używaj tytułu „Projekt i implementacja systemu kolejkowania wiadomości”, dopóki użytkownik nie poda zatwierdzonego wariantu.
- Pisz po polsku, chyba że użytkownik poprosi o abstrakt lub fragment w innym języku.
- Traktuj repozytorium jako materiał badawczy, a nie gotową narrację. Sprawdzaj każde twierdzenie o systemie w aktualnym kodzie, testach lub wynikach uruchomień.
- Nie wymyślaj funkcji, wyników, źródeł, cytowań, wymagań uczelni ani wkładu autora. Oznaczaj brak danych i stosuj jawne placeholdery, np. `[ŹRÓDŁO DO UZUPEŁNIENIA]`.
- Rozróżniaj stan `zaimplementowany`, `zweryfikowany`, `zaakceptowany projektowo`, `proponowany` i `niezweryfikowany`.
- Nie przypisuj użytkownikowi autorstwa konkretnej części kodu bez potwierdzenia. W razie braku danych stosuj neutralny opis.
- Parafrazuj źródła i cytuj cudze idee. Nie przedstawiaj wygenerowanego tekstu jako substytutu samodzielnej weryfikacji ani wymagań promotora i uczelni.
- Odróżniaj lokalny projekt RocketMQ od produktu Apache RocketMQ. Nie przenoś cech Apache RocketMQ na ten system bez dowodu.

## Ustal zakres i bazę dowodową

1. Znajdź katalog główny repozytorium i przeczytaj `AGENTS.md` oraz `CLAUDE.md`.
2. Zapisz `git status --short`, bieżący commit i datę analizy. Nie mieszaj niezatwierdzonych zmian z udokumentowanym stanem bez wyraźnej adnotacji.
3. Ustal oczekiwany artefakt: konspekt, fragment rozdziału, pełny rozdział, diagram, opis eksperymentu, bibliografia albo recenzja.
4. Ustal znane wytyczne: limit objętości, szablon uczelni, styl cytowań, wymagany czas i osoba gramatyczna. Jeżeli ich brakuje, przyjmij roboczą strukturę i nazwij założenia.
5. Przeczytaj [mapę dowodów projektu](references/project-evidence-map.md). Przy planowaniu całej pracy lub rozdziału przeczytaj także [model rozdziałów](references/chapter-blueprint.md).
6. Skopiuj w razie potrzeby [rejestr dowodów](assets/evidence-register-template.md) i powiąż planowane tezy z konkretnymi źródłami.
7. Otwórz dokładne pliki implementacji, testy i dokumenty dotyczące opisywanego mechanizmu. Nie opieraj szczegółowego opisu tylko na `README.md` lub nazwach typów.
8. Uruchom proporcjonalne testy albo eksperyment tylko wtedy, gdy wniosek wymaga świeżego wyniku. Zapisz polecenie, środowisko, commit, konfigurację i surowy rezultat.

Stosuj hierarchię dowodów:

1. wykonywalny kod, konfiguracja uruchomieniowa i odtworzony wynik;
2. testy potwierdzające konkretny kontrakt;
3. zaakceptowane ADR-y opisujące intencję i kompromisy;
4. aktualna dokumentacja użytkowa i architektoniczna;
5. otwarte decyzje oraz plany, wyłącznie jako propozycje;
6. literatura zewnętrzna dla teorii, standardów i porównań.

Jeżeli źródła są sprzeczne, opisz rozbieżność. Nie wygładzaj jej w narracji.

## Dobierz tryb pracy

### Planowanie pracy

- Sformułuj problem, cel główny, cele szczegółowe, zakres, ograniczenia i kryteria oceny.
- Powiąż każdy rozdział z pytaniem, na które odpowiada, oraz dowodem potrzebnym do odpowiedzi.
- Użyj [modelu rozdziałów](references/chapter-blueprint.md) jako punktu wyjścia, nie jako obowiązkowego szablonu uczelni.
- Oddziel wiedzę teoretyczną od opisu własnego rozwiązania i od wyników badań.

### Pisanie rozdziału

- Zacznij od celu rozdziału i krótkiego konspektu, jeśli użytkownik nie podał struktury.
- Buduj akapity według schematu: teza, dowód lub źródło, interpretacja, związek z celem pracy.
- Wyjaśniaj decyzje projektowe wraz z alternatywami i konsekwencjami; nie twórz katalogu klas bez argumentacji.
- Cytuj kod oszczędnie. Preferuj pseudokod, diagram przepływu i krótkie, istotne fragmenty z opisem.
- Utrzymuj jednolite terminy: broker, komunikat, wymiana, kolejka, wiązanie, klucz routingu, dzierżawa, potwierdzenie i ponowne kolejkowanie.
- Przed oddaniem wykonaj kontrolę z [zasad źródeł i cytowań](references/source-and-citation-policy.md).

### Opis architektury i implementacji

- Wyprowadź diagram z aktualnych zależności projektów oraz przepływu wywołań.
- Sprawdź granice architektury heksagonalnej w kodzie i testach architektonicznych.
- Dla każdego mechanizmu opisz odpowiedzialność, wejście, wyjście, stan, błędy, współbieżność i kompromisy.
- Dla semantyki dostarczania opisz scenariusz sukcesu oraz awarie: timeout dzierżawy, `Ack`, `Nack`, ponowne dostarczenie i dead-letter, ale tylko w zakresie potwierdzonym implementacją.
- Nie nazywaj adaptera produkcyjnym ani trwałym wyłącznie dlatego, że istnieje projekt lub interfejs.

### Testy i ocena rozwiązania

- Oddziel metodykę, środowisko, wyniki, interpretację, zagrożenia trafności i ograniczenia.
- Powiąż wymagania z testami jednostkowymi, kontraktowymi, architektonicznymi, integracyjnymi i transportowymi.
- Nie twórz danych wydajnościowych. Korzystaj wyłącznie z zachowanych artefaktów lub świeżych, powtarzalnych pomiarów.
- Przy benchmarkach podaj sprzęt, system, runtime, konfigurację, rozmiar wiadomości, obciążenie, czas rozgrzewki, liczbę prób i miary rozrzutu.
- Nie wyciągaj wniosków przyczynowych wyłącznie z throughputu i percentyli opóźnień.

### Przegląd i korekta

- Sprawdź śledzenie: cel → wymaganie → decyzja → implementacja → test → wniosek.
- Wyszukaj twierdzenia absolutne, nieudokumentowane liczby, zmienną terminologię, brakujące źródła i opisy planów zapisane w czasie dokonanym.
- Sprawdź, czy wnioski odpowiadają wynikom, a podsumowanie rozlicza cele z wprowadzenia.
- Zachowaj styl autora; poprawiaj precyzję, logikę i język bez sztucznego napompowywania tekstu.

## Zarządzaj źródłami

Przeczytaj [zasady źródeł i cytowań](references/source-and-citation-policy.md) przed tworzeniem bibliografii, porównań z innymi systemami lub części teoretycznej. Preferuj źródła pierwotne: standardy, dokumentację producentów technologii i publikacje naukowe. Dla każdej pozycji zapisz pełne dane bibliograficzne oraz datę dostępu, jeśli wymaga jej wybrany styl.

Nie generuj pozornie kompletnych rekordów bibliograficznych z pamięci. Gdy źródło nie zostało sprawdzone, pozostaw oznaczony placeholder zamiast fałszywego cytowania.

## Zwracaj rezultat możliwy do zweryfikowania

Jeśli użytkownik nie zażąda czystej wersji finalnej, zwróć:

1. tekst lub plan w ustalonym formacie;
2. krótką tabelę `teza → dowód` poza tekstem pracy;
3. listę brakujących źródeł, wyników albo decyzji;
4. założenia i ograniczenia wersji roboczej.

Do dłuższych fragmentów można skopiować [szablon rozdziału](assets/chapter-draft-template.md). Przed oznaczeniem tekstu jako gotowego usuń placeholdery dopiero po ich rzeczywistym uzupełnieniu i ponownie sprawdź wszystkie twierdzenia o implementacji.
