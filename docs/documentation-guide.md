# Plan i zasady pisania dokumentacji

Ten dokument jest instrukcją utrzymania całej dokumentacji RocketMQ. Określa strukturę, źródła prawdy, styl oraz definicję ukończenia zmiany.

## Model informacji

Stosujemy cztery typy stron:

| Typ | Pytanie czytelnika | Zasada |
|---|---|---|
| tutorial | „Jak przejść pierwszy działający scenariusz?” | jedna ścieżka, kompletne kroki i oczekiwany wynik |
| how-to | „Jak wykonać konkretne zadanie?” | cel, warunki, polecenia, pułapki; bez rozbudowanej teorii |
| explanation | „Dlaczego system działa w ten sposób?” | mechanizmy, kompromisy i zależności |
| reference | „Jaka jest dokładna wartość lub sygnatura?” | zwięzłe tabele odzwierciedlające kod |

Raport w <code>docs/report</code> jest piątą warstwą: tworzy ciągłą narrację techniczną i odsyła do dowodów. ADR zachowuje decyzję w czasie, dlatego nie zastępuje reference.

## Źródła prawdy

Kolejność weryfikacji twierdzenia:

1. publiczny kontrakt protobuf albo interfejs Core;
2. implementacja aktywnie rejestrowana w Runnerze;
3. test kontraktowy lub integracyjny;
4. aktualny ADR dla uzasadnienia decyzji;
5. dokumentacja zewnętrzna producenta technologii.

Plik Proposed albo plan implementacyjny nie dowodzi, że funkcja działa. Jeśli kod i ADR się różnią, zachowaj historyczny status ADR i dodaj datowaną notę implementacyjną. Nie podejmuj otwartej decyzji tylko po to, aby dokument był pozornie spójny.

## Jak napisać stronę

1. Określ odbiorcę i jeden typ strony.
2. Zapisz jedno zdanie celu na początku.
3. Zbierz dowody w kodzie i testach przed napisaniem opisu.
4. Używaj terminów ze [słownika](reference/slownik.md).
5. Dodaj najmniejszy potrzebny przykład; przykład publicznego API powinien się kompilować.
6. Linkuj do wyjaśnienia zamiast powtarzać teorię w how-to.
7. Oznacz ograniczenia i elementy niezaimplementowane.
8. Uruchom walidację i testy.

## Szablony

### Tutorial

~~~markdown
# Osiągnij rezultat

Krótko opisz wynik.

## Wymagania
## 1. Pierwszy krok
## 2. Następny krok
## Oczekiwany wynik
## Następne kroki
~~~

### How-to

~~~markdown
# Wykonaj zadanie

Jedno zdanie celu.

## Warunki wstępne
## Procedura
## Sprawdzenie wyniku
## Typowe problemy
~~~

### Explanation

~~~markdown
# Mechanizm

## Problem
## Jak działa w RocketMQ
## Konsekwencje i kompromisy
## Powiązane elementy
~~~

### Reference

~~~markdown
# Nazwa kontraktu

Krótki zakres i wersja.

## Sygnatury lub pola
## Wartości domyślne
## Błędy
~~~

## Styl

Pisz po polsku, konkretnie i w stronie czynnej. Nazwy typów i wartości kodowych zachowuj po angielsku. Najpierw podawaj zachowanie projektu, potem niezbędne tło .NET. Nie nazywaj kanału trwałą kolejką. Nie obiecuj exactly-once, produkcyjnej gotowości ani funkcji planowanych.

Diagram dodawaj tylko wtedy, gdy pokazuje przepływ, stan albo zależności lepiej niż akapit. Polecenia PowerShell oznaczaj językiem, a nazwy plików zapisuj małymi literami z łącznikami.

## Macierz aktualizacji

| Zmiana | Dokumenty do sprawdzenia |
|---|---|
| protobuf lub publiczny SDK | grpc-api, dotnet-sdk, błędy, tutorial, przykład |
| routing | routing, model domenowy, status, ADR-0002 |
| lease, Ack, Nack, dead-letter | semantyka dostarczania, Core API, decyzja 02 |
| SQLite lub migracja | publikacja i SQLite, schemat, konfiguracja, decyzja 03 |
| hosting lub port | README, tutorial, konfiguracja, deployment decision |
| test lub benchmark | testy i benchmarki, raport weryfikacji, rejestr dowodów |

## Definicja ukończenia

Zmiana dokumentacji jest ukończona, gdy lokalne linki przechodzą walidację, snippet publicznego API kompiluje się albo odsyła do kompilowanego przykładu, twierdzenia raportu mają dowód, a build i testy kończą się sukcesem.

~~~powershell
./tools/verify-docs.ps1
dotnet build --no-restore
dotnet test --no-build --verbosity normal
~~~

## Dalszy plan

Struktura bazowa jest wdrożona. Następne iteracje powinny dodawać dokumentację operacyjną razem z implementacją TLS/auth, health checks, backup/restore i telemetryki. Po uzyskaniu porównywalnych artefaktów benchmarku należy uzupełnić rozdział wydajności rzeczywistymi wynikami, bez zastępowania pomiaru estymacją.

