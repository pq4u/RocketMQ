# Dokumentacja RocketMQ

Ta dokumentacja opisuje lokalny projekt RocketMQ: jednowęzłowy broker wiadomości dla .NET 10. Wybierz stronę według tego, czy chcesz wykonać zadanie, nauczyć się przepływu, zrozumieć mechanizm, czy sprawdzić kontrakt.

## Tutorial

Tutorial prowadzi jedną sprawdzoną ścieżką od uruchomienia brokera do potwierdzenia komunikatu.

- [Wyślij i odbierz pierwszy komunikat](tutorials/pierwszy-komunikat.md).

## Instrukcje how-to

Instrukcje zakładają, że znasz podstawowy przepływ i chcesz wykonać konkretne zadanie.

- [Uruchom broker z SQLite](how-to/uruchom-broker.md).
- [Skonfiguruj SQLite i batching publikacji](how-to/skonfiguruj-persistence.md).
- [Publikuj komunikaty idempotentnie](how-to/publikuj-idempotentnie.md).
- [Zaimplementuj konsumenta](how-to/zaimplementuj-konsumenta.md).
- [Obsłuż ponowne dostarczenie i błędy](how-to/obsluz-redelivery.md).
- [Uruchom benchmark publikacji](how-to/uruchom-benchmark.md).

## Wyjaśnienia

Wyjaśnienia łączą teorię systemów kolejkowych i mechanizmy .NET z konkretnymi elementami RocketMQ.

- [Model brokera wiadomości](explanation/model-brokera.md).
- [Architektura heksagonalna](explanation/architektura.md).
- [Model domenowy i identyfikatory](explanation/model-domenowy.md).
- [Routing direct, fanout i topic](explanation/routing.md).
- [Semantyka dostarczania](explanation/semantyka-dostarczania.md).
- [Atomowa publikacja i trwałość SQLite](explanation/publikacja-i-sqlite.md).
- [Asynchroniczność, kanały i współbieżność w .NET](explanation/dotnet-asynchronicznosc.md).
- [Generic Host, DI i cykl życia](explanation/dotnet-hosting.md).
- [gRPC i Protocol Buffers](explanation/grpc-protobuf.md).
- [Strategia testów i pomiarów](explanation/testy-i-benchmarki.md).

## Reference

Reference odzwierciedla bieżące kontrakty kodu. Użyj go do sprawdzania parametrów, wartości domyślnych i błędów.

- [Konfiguracja brokera](reference/konfiguracja.md).
- [API gRPC](reference/grpc-api.md).
- [SDK .NET](reference/dotnet-sdk.md).
- [Porty i modele Core](reference/core-api.md).
- [Schemat SQLite](reference/sqlite-schema.md).
- [Błędy i ich obsługa](reference/bledy.md).
- [CLI benchmarku](reference/benchmark-cli.md).
- [Status funkcji i ograniczenia](reference/status-funkcji.md).
- [Słownik pojęć](reference/slownik.md).

## Raport i decyzje

Raport syntetyzuje wiedzę, a ADR-y zachowują historię decyzji.

- [Projekt i implementacja systemu kolejkowania wiadomości](report/index.md).
- [Rejestr dowodów raportu](report/rejestr-dowodow.md).
- [ADR-y](adr/).
- [Otwarte decyzje projektowe](decisions/README.md).
- [Plany implementacyjne](plans/README.md).

## Rozwijanie dokumentacji

- [Plan i zasady pisania dokumentacji](documentation-guide.md).
- Walidacja lokalnych linków: <code>./tools/verify-docs.ps1</code>.
