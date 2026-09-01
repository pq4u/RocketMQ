# Model rozdziałów pracy

Model dotyczy tytułu „Projekt i implementacja systemu kolejkowania wiadomości”. Dopasuj numerację, obowiązkowe elementy i objętość do zatwierdzonego szablonu uczelni.

## Elementy wstępne

- streszczenie po polsku: problem, metoda, rezultat i najważniejszy wniosek;
- abstract po angielsku o tej samej treści merytorycznej;
- słowa kluczowe zgodne z faktycznym zakresem;
- spis treści, wykaz skrótów, rysunków i tabel, jeżeli są wymagane.

Nie pisz streszczenia jako listy obietnic. Uzupełnij je po uzyskaniu wyników.

## 1. Wprowadzenie

Odpowiedz na pytania:

1. Jaki problem rozwiązuje kolejkowanie wiadomości?
2. Dlaczego zasadne jest zaprojektowanie własnego prototypu?
3. Jaki jest cel główny i mierzalne cele szczegółowe?
4. Co należy do zakresu, a co świadomie poza nim pozostaje?
5. Jaką metodą zaprojektowano, zaimplementowano i oceniono rozwiązanie?
6. Jak zorganizowano dalszą część pracy?

Przykładowy cel roboczy: zaprojektować, zaimplementować i zweryfikować prototyp brokera wiadomości z nazwanymi kolejkami, routingiem opartym na wymianach, jawnym potwierdzaniem odbioru i transportem gRPC. Przed użyciem dopasuj go do faktycznie ukończonego zakresu.

## 2. Podstawy teoretyczne i technologie

Rozważ podrozdziały:

- komunikacja synchroniczna i asynchroniczna;
- rola brokera oraz modele point-to-point i publish/subscribe;
- kolejki, wymiany, wiązania i klucze routingu;
- semantyki dostarczania, idempotencja i obsługa błędów;
- backpressure, współbieżność i porządkowanie;
- trwałość, dziennik WAL i transakcje;
- gRPC, Protocol Buffers, HTTP/2 i użyte elementy platformy .NET.

Każde pojęcie teoretyczne oprzyj na źródle zewnętrznym. Kończ podrozdziały wskazaniem, jak teoria wpływa na decyzje projektu.

## 3. Analiza wymagań i projekt systemu

Uwzględnij:

- interesariuszy i przypadki użycia producenta, konsumenta i administratora;
- wymagania funkcjonalne z identyfikatorami, np. `RF-01`;
- wymagania niefunkcjonalne z mierzalnym kryterium, np. `RNF-03`;
- założenia i ograniczenia prototypu;
- model domenowy oraz odpowiedzialności komponentów;
- architekturę heksagonalną i granice portów/adaptatorów;
- sekwencję publikacji, routingu, leasingu i potwierdzenia;
- rozważone alternatywy i uzasadnienie decyzji z ADR-ów.

Wymaganie nie jest wynikiem. Każde kryterium powinno wskazywać późniejszy test lub pomiar.

## 4. Implementacja

Możliwy układ:

1. organizacja rozwiązania i zależności projektów;
2. modele i porty warstwy Core;
3. routing direct, fanout i topic;
4. kanał ograniczony i propagacja backpressure;
5. model lease, ack, nack, redelivery i dead-letter;
6. kontrakt oraz usługi gRPC;
7. biblioteka kliencka producenta, konsumenta i administracji;
8. adapter używany do trwałości danych;
9. konfiguracja i składanie procesu w runnerze;
10. obsługa błędów, anulowania i zamykania procesu.

Dla każdego elementu wyjaśnij powód, mechanizm, kompromis i dowód działania. Usuń elementy, których aktualny kod nie realizuje.

## 5. Weryfikacja i ocena

Zbuduj rozdział w kolejności:

1. pytania badawcze lub kryteria akceptacji;
2. środowisko i wersja badanego kodu;
3. strategia testów;
4. scenariusze oraz dane wejściowe;
5. wyniki poprawności;
6. metodyka benchmarku;
7. wyniki wydajności z miarami rozrzutu;
8. interpretacja względem wymagań;
9. ograniczenia eksperymentu i zagrożenia trafności.

Przykładowe pytania do dopasowania:

- Czy routing kieruje komunikat do dokładnie tych kolejek, które spełniają regułę wiązania?
- Czy niepotwierdzony komunikat może zostać ponownie dostarczony po wygaśnięciu dzierżawy?
- Czy adaptery przestrzegają tego samego kontraktu zachowania?
- Jak obciążenie i topologia wpływają na przepustowość oraz percentyle opóźnienia w określonym środowisku?

Nie deklaruj odpowiedzi przed analizą danych.

## 6. Podsumowanie

- Rozlicz każdy cel i wymaganie, wskazując dowód.
- Wymień rzeczywisty wkład projektowy i implementacyjny tylko w potwierdzonym zakresie.
- Oddziel ograniczenia prototypu od błędów i od planów rozwoju.
- Formułuj przyszłe prace konkretnie, bez przedstawiania ich jako funkcji istniejących.

## Dodatki

Rozważ umieszczenie poza główną narracją:

- pełnej tabeli wymagań i ich śledzenia;
- większych fragmentów kontraktu protobuf;
- instrukcji odtworzenia środowiska;
- szczegółowych tabel pomiarowych;
- dodatkowych diagramów sekwencji;
- informacji o analizowanym commicie i konfiguracji.

Nie przenoś do dodatków informacji koniecznych do zrozumienia głównego wniosku.
