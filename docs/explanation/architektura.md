# Architektura

Projekt stosuje architekturę portów i adapterów. <code>RocketMQ.Core</code> definiuje model i kontrakty, a transport oraz mechanizmy trwałości zależą od Core, nigdy odwrotnie.

~~~mermaid
flowchart TB
    SDK[RocketMQ.Client] --> GRPC[RocketMQ.Transport.Grpc]
    GRPC --> PORTS[Porty RocketMQ.Core]
    ROUTER[Routing RocketMQ.Core] --> PORTS
    SQLITE[RocketMQ.Persistence.Sqlite] --> PORTS
    WAL[RocketMQ.Persistence.Wal - szkielet] --> PORTS
    RUNNER[RocketMQ.Runner] --> GRPC
    RUNNER --> SQLITE
    RUNNER --> ROUTER
~~~

## Odpowiedzialności projektów

| Projekt | Odpowiedzialność |
|---|---|
| <code>RocketMQ.Core</code> | modele, porty, matcher topic i router |
| <code>RocketMQ.Client</code> | publiczny SDK producenta, konsumenta i administracji |
| <code>RocketMQ.Transport.Grpc</code> | kontrakt protobuf, usługi gRPC i mapowanie błędów |
| <code>RocketMQ.Persistence.Sqlite</code> | topologia, publikowanie transakcyjne, leasing i retencja |
| <code>RocketMQ.Persistence.Wal</code> | eksperymentalny, niezaimplementowany adapter |
| <code>RocketMQ.Runner</code> | composition root, konfiguracja i cykl życia procesu |

## Przepływ publikacji

Usługa gRPC waliduje żądanie i wywołuje <code>IMessagePublisher</code>. Implementacja SQLite umieszcza pracę w ograniczonym kanale, pojedynczy worker zbiera batch, a jedna transakcja sprawdza identyfikator publikacji, wyznacza kolejki, zapisuje publikację oraz osobne wiersze wiadomości i dopiero po zatwierdzeniu kończy oczekujące wywołanie.

## Przepływ odbioru

SDK odpytuje unary RPC <code>LeaseNext</code>. Store atomowo wybiera najstarszy dostępny komunikat, ustawia identyfikator i termin lease oraz zwiększa licznik dostarczeń. Handler kończy pracę przez <code>Ack</code> albo <code>Nack</code>.

## Granice zależności

Testy architektury pilnują, aby Core nie zależał od transportu i adapterów. Composition root zna typy konkretne, ponieważ jego rolą jest połączenie portów z implementacjami. To dlatego rejestracja DI znajduje się w Runnerze, a nie w Core.

Aktualny stan funkcji opisuje [macierz statusu](../reference/status-funkcji.md). Decyzje i rozbieżności projektowe znajdują się w [ADR](../adr/) oraz [rejestrze decyzji](../decisions/).

