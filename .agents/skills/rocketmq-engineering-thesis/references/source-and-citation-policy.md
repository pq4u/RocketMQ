# Zasady źródeł i cytowań

## Rozdziel dwa rodzaje dowodów

### Źródła wewnętrzne projektu

Kod, testy, ADR-y, dokumentacja i artefakty benchmarków dowodzą stanu badanego projektu. Zapisuj:

- identyfikator commitu;
- ścieżkę i symbol albo sekcję dokumentu;
- status pliku w drzewie roboczym;
- datę odczytu lub wykonania;
- polecenie i środowisko dla wyników dynamicznych.

W tekście pracy odwołuj się do repozytorium zgodnie z zasadami uczelni, a ścieżki techniczne umieszczaj w przypisie, podpisie rysunku, dodatku albo rejestrze dowodów. Sam numer linii jest nietrwały; łącz go z commitem i nazwą symbolu.

### Źródła zewnętrzne

Źródła zewnętrzne uzasadniają definicje, standardy, mechanizmy technologii i porównania. Preferuj w tej kolejności:

1. standard, RFC lub specyfikację;
2. recenzowaną publikację naukową;
3. oficjalną dokumentację technologii;
4. książkę techniczną uznanego wydawnictwa;
5. materiał wtórny tylko wtedy, gdy lepszego źródła nie ma.

Dla .NET, gRPC, Protocol Buffers i SQLite korzystaj przede wszystkim z dokumentacji ich autorów. Dla pojęć systemów rozproszonych korzystaj z publikacji naukowych, standardów i literatury akademickiej.

## Nie pomyl projektu z Apache RocketMQ

Wyniki wyszukiwania hasła „RocketMQ” często dotyczą Apache RocketMQ. Użyj ich tylko w świadomym porównaniu i nazwij produkt pełną nazwą. Nie cytuj dokumentacji Apache RocketMQ jako dowodu zachowania lokalnego projektu.

## Zbuduj rekord źródła przed cytowaniem

Zapisz co najmniej:

- autora lub organizację;
- pełny tytuł;
- rok i wydawcę albo nazwę serwisu;
- DOI, ISBN lub stabilny URL, jeśli istnieje;
- numer wersji dokumentacji, jeżeli ma znaczenie;
- datę dostępu, gdy wymaga tego styl;
- konkretną tezę, którą źródło wspiera.

Nie uzupełniaj brakujących pól na podstawie domysłu. Użyj `[BRAK: ...]` i poproś o weryfikację.

## Dopasuj cytowanie do twierdzenia

| Typ twierdzenia | Wymagany dowód |
|---|---|
| definicja lub ogólna właściwość technologii | źródło zewnętrzne |
| decyzja architektoniczna projektu | zaakceptowany ADR i zgodność z kodem |
| zachowanie implementacji | kod oraz odpowiedni test lub wykonanie |
| wartość liczbowa | surowy wynik, konfiguracja i metodologia |
| porównanie z innym brokerem | aktualne źródło dla obu stron i wspólne kryteria |
| wkład autora | potwierdzenie użytkownika oraz historia projektu, jeśli dostępna |

Jedno źródło nie musi wspierać całego akapitu. Umieszczaj odwołanie blisko tezy, której dotyczy.

## Pisz uczciwie

- Preferuj parafrazę ze wskazaniem źródła.
- Cytat dosłowny oznacz cudzysłowem i numerem strony lub sekcji.
- Nie składaj tekstu z bliskich parafraz dokumentacji.
- Nie twórz bibliografii z niezweryfikowanych tytułów, DOI ani adresów.
- Nie ukrywaj wyniku przeczącego tezie pracy.
- Sprawdź zasady uczelni dotyczące deklarowania użycia narzędzi generatywnych.

## Pracuj z placeholderami

W wersji roboczej stosuj jednoznaczne oznaczenia:

- `[CIT-01: źródło definicji backpressure]`;
- `[EVID-07: wynik testu kontraktowego]`;
- `[BRAK: konfiguracja sprzętowa benchmarku]`;
- `[DO WERYFIKACJI: status implementacji WAL]`.

Przed oddaniem tekstu wyszukaj `CIT-`, `EVID-`, `BRAK:` i `DO WERYFIKACJI`. Usuń znacznik tylko po uzupełnieniu i sprawdzeniu dowodu.

## Dostosuj styl bibliografii

Nie wybieraj arbitralnie APA, IEEE, PN-ISO 690 ani stylu wydziałowego. Najpierw sprawdź wytyczne lub szablon uczelni. Utrzymuj jeden styl w całej pracy, łącznie z kolejnością autorów, zapisem dat, wielkością liter, identyfikatorami i datami dostępu.
