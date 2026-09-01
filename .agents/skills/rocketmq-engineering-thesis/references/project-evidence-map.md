# Mapa dowodów projektu RocketMQ

Ta mapa wskazuje, gdzie szukać dowodów. Nie zastępuje odczytu aktualnych plików. Przed użyciem sprawdź bieżący commit, różnice robocze oraz rzeczywistą zawartość wskazanych ścieżek.

## Klasyfikacja stanu

| Etykieta | Minimalny dowód | Dozwolone sformułowanie |
|---|---|---|
| Zaimplementowane | wykonywalna ścieżka w kodzie | „System implementuje…” |
| Zweryfikowane | implementacja i adekwatny test lub odtworzony wynik | „Testy potwierdzają…” |
| Zaakceptowane projektowo | zaakceptowany ADR | „W projekcie przyjęto…” |
| Proponowane | otwarta decyzja lub plan | „Rozważane jest…” |
| Zmierzone | surowy artefakt, środowisko i procedura | „W badanym środowisku uzyskano…” |
| Niezweryfikowane | brak wystarczającego dowodu | „Nie potwierdzono…” |

Nie zmieniaj etykiety na mocniejszą na podstawie samej nazwy klasy, obecności projektu albo komentarza `TODO`.

## Źródła przekrojowe

| Obszar pracy | Zacznij od | Potwierdź w |
|---|---|---|
| Cel i zakres prototypu | `README.md`, `docs/architecture.md` | kod uruchomieniowy, testy, bieżące ograniczenia |
| Reguły architektury | `AGENTS.md`, `CLAUDE.md`, `docs/architecture.md` | zależności `.csproj`, `tests/RocketMQ.Architecture.Tests` |
| Decyzje projektowe | `docs/adr/` | aktualny kod i testy; sprawdź status ADR-u |
| Nierozstrzygnięte kierunki | `docs/decisions/`, `docs/plans/` | status dokumentu i historia zmian |
| Kontrakt zewnętrzny | `src/Transport/RocketMQ.Transport.Grpc/Protos/rocketmq.proto` | serwisy gRPC, testy transportu, klient SDK |
| Konfiguracja i uruchomienie | `src/Runner/RocketMQ.Runner/Program.cs`, `appsettings.json` | `docs/getting-started.md`, test uruchomieniowy |
| Wyniki wydajności | `artifacts/benchmarks/` | `tools/RocketMQ.Benchmark`, jego README i środowisko pomiaru |
| Ewolucja projektu | `git log`, `git show`, `git blame` | kod i dokumenty z analizowanego commitu |

## Model domeny i routing

- Porty systemu: `src/Core/RocketMQ.Core/Abstractions/`.
- Typy domenowe: `src/Core/RocketMQ.Core/Models/`.
- Dopasowanie tematów i rozwiązywanie tras: `src/Core/RocketMQ.Core/Routing/`.
- Intencja modelu kolejki: `docs/adr/0001-queue-over-log.md`.
- Model wymian, kolejek, wiązań i kluczy routingu: `docs/adr/0002_routing_architecture.md`.
- Dowody zachowania: `tests/RocketMQ.Runner.Unit.Tests/MessageRouterTests.cs`, `TopicMatcherTests.cs`, `DeliverySemanticsTests.cs` oraz kontrakty w `tests/RocketMQ.Contract.Tests/`.

Przy opisie przepływu śledź wiadomość od kontraktu transportowego przez mapowanie na typ domenowy, kanał i router do wybranych kolejek. Sprawdź, czy każda opisywana krawędź istnieje w aktualnym kodzie.

## Semantyka dostarczania

Sprawdź razem:

- `IMessageQueueStore` i modele `LeasedMessage` oraz `DeadLetteredMessage`;
- implementację używaną przez runner;
- testy kontraktowe magazynu kolejki;
- testy integracyjne i transportowe dotyczące consume, ack i nack;
- `docs/decisions/02-delivery-semantics.md` oraz ADR-y związane z modelem kolejki.

Nie utożsamiaj istnienia kontraktu z pełną implementacją wszystkich adapterów. Dla określeń `at-most-once`, `at-least-once` i `exactly-once` podaj precyzyjny zakres oraz scenariusz awarii.

## Transport i klient

- Kontrakt protobuf: `src/Transport/RocketMQ.Transport.Grpc/Protos/rocketmq.proto`.
- Implementacja serwera: `src/Transport/RocketMQ.Transport.Grpc/`.
- Producent, konsument i administracja: `src/Client/RocketMQ.Client/`.
- Składanie procesu: `src/Runner/RocketMQ.Runner/Program.cs`.
- Decyzje: `docs/adr/0003_grpc_transport_layer.md` i `docs/adr/0004_client_sdk_architecture.md`.
- Testy: `tests/RocketMQ.Transport.Grpc.Tests/`.

Zweryfikuj osobno protokół, mapowanie błędów, propagację anulowania, backpressure, retry klienta i cykl życia konsumenta. Nie opisuj HTTP/2, gRPC ani protobuf jako własnych wynalazków projektu.

## Trwałość danych

- Adapter SQLite: `src/Persistence/RocketMQ.Persistence.Sqlite/`.
- Adapter własnego WAL: `src/Persistence/RocketMQ.Persistence.Wal/`.
- Decyzja i kryteria: `docs/decisions/03-persistence-strategy.md`.
- Kontrakty: `tests/RocketMQ.Contract.Tests/`.
- Integracja SQLite: `tests/RocketMQ.Runner.Unit.Tests/SqlitePersistenceIntegrationTests.cs`.

Przed każdym opisem sprawdź, który adapter jest faktycznie składany w runnerze, które metody są ukończone, które rzucają `NotImplementedException`, jakie są ustawienia transakcji i durability oraz jakie przypadki awarii zostały przetestowane. Nie wnioskuj o odporności na awarię z samego użycia SQLite lub pliku WAL.

## Testowanie i pomiary

Rozdziel zestawy dowodów:

- testy jednostkowe logiki routingu i semantyki;
- testy kontraktowe wspólne dla adapterów;
- testy architektoniczne zależności;
- testy usług gRPC i przepływu in-process;
- testy integracyjne persistence;
- benchmark jako eksperyment, nie test poprawności.

Dla wyników `dotnet test` zapisz dokładne polecenie, commit, SDK, liczbę testów, rezultat i datę. Dla benchmarku nie agreguj plików z różnych konfiguracji jako jednej próby. Surowe pliki JSON zachowuj bez modyfikacji.

## Terminologia do utrzymania

| Termin | Znaczenie w tej pracy |
|---|---|
| broker | autorski system pośredniczący w przekazywaniu komunikatów |
| exchange / wymiana | element rozstrzygający routing do kolejek |
| queue / kolejka | nazwana jednostka przechowująca komunikaty dla konkurujących konsumentów |
| binding / wiązanie | relacja wymiany i kolejki z regułą routingu |
| routing key / klucz routingu | wartość używana przy wyborze wiązań |
| lease / dzierżawa | czasowe przekazanie komunikatu konsumentowi przed potwierdzeniem |
| ack | pozytywne potwierdzenie przetworzenia |
| nack | negatywne potwierdzenie z decyzją o ponownym kolejkowaniu |
| backpressure | kontrolowane ograniczenie napływu przy braku pojemności dalszego etapu |

Przy pierwszym użyciu terminu angielskiego podaj polski odpowiednik lub krótką definicję, a potem stosuj jeden wariant konsekwentnie.
