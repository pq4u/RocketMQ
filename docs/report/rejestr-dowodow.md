# Rejestr dowodów

Rejestr łączy twierdzenia raportu z artefaktem możliwym do sprawdzenia. Ścieżki odnoszą się do bieżącego repozytorium.

| ID | Twierdzenie | Dowód |
|---|---|---|
| EVID-001 | Core nie zależy od adapterów | [testy architektury](../../tests/RocketMQ.Architecture.Tests/) |
| EVID-002 | Runner używa SQLite i wymaga lokalnej ścieżki bezwzględnej | [Program.cs](../../src/Runner/RocketMQ.Runner/Program.cs) |
| EVID-003 | Publish jest portem trwałym | [IMessagePublisher.cs](../../src/Core/RocketMQ.Core/Abstractions/IMessagePublisher.cs) |
| EVID-004 | Lease, Ack, Nack, FIFO i at-least-once mają jawny kontrakt | [IMessageQueueStore.cs](../../src/Core/RocketMQ.Core/Abstractions/IMessageQueueStore.cs) |
| EVID-005 | Routing metadata ma kontrakt idempotencji i integralności | [IRoutingStore.cs](../../src/Core/RocketMQ.Core/Abstractions/IRoutingStore.cs) |
| EVID-006 | Kanał publishera jest bounded i ma jednego readera | [SqliteMessagePublisher.cs](../../src/Persistence/RocketMQ.Persistence.Sqlite/SqliteMessagePublisher.cs) |
| EVID-007 | Publikacja jest zapisywana transakcyjnie i ma fingerprint | [SqliteMessagePublisher.cs](../../src/Persistence/RocketMQ.Persistence.Sqlite/SqliteMessagePublisher.cs) |
| EVID-008 | Baza używa WAL, FULL, FK i busy timeout | [SqliteDatabase.cs](../../src/Persistence/RocketMQ.Persistence.Sqlite/SqliteDatabase.cs) |
| EVID-009 | Schemat ma wersje migracji 1 i 2 | [SqliteDatabase.cs](../../src/Persistence/RocketMQ.Persistence.Sqlite/SqliteDatabase.cs) |
| EVID-010 | Kontrakt sieciowy ma trzy usługi unary | [rocketmq.proto](../../src/Transport/RocketMQ.Transport.Grpc/Protos/rocketmq.proto) |
| EVID-011 | SDK konsumenta mapuje wyniki na Ack i Nack | [Consumer.cs](../../src/Client/RocketMQ.Client/Consumer.cs) |
| EVID-012 | SDK producenta retryuje ResourceExhausted | [Producer.cs](../../src/Client/RocketMQ.Client/Producer.cs) |
| EVID-013 | WAL nie jest zaimplementowany | [adapter WAL](../../src/Persistence/RocketMQ.Persistence.Wal/) |
| EVID-014 | Przykład publicznej ścieżki SDK jest częścią builda | [RocketMQ.Example](../../examples/RocketMQ.Example/) |
| EVID-015 | Benchmark zapisuje raport środowiska i percentyle | [narzędzie benchmarkowe](../../tools/RocketMQ.Benchmark/) |

## Źródła zewnętrzne

- [.NET Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) — zachowanie kanałów ograniczonych i trybów zapełnienia.
- [.NET Generic Host](https://learn.microsoft.com/en-gb/dotnet/core/extensions/generic-host) — cykl życia, konfiguracja i hosted services.
- [.NET dependency injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview) — model kontenera i rejestracji.
- [gRPC core concepts](https://grpc.io/docs/what-is-grpc/core-concepts/) — rodzaje RPC i model usług.
- [Protocol Buffers proto3](https://protobuf.dev/programming-guides/proto3/) — kontrakt i reguły ewolucji pól.
- [SQLite WAL](https://sqlite.org/wal.html) — właściwości i ograniczenia trybu WAL.
- [SQLite transactions](https://sqlite.org/lang_transaction.html) — atomowość i model transakcji.
- [SQLite synchronous](https://sqlite.org/pragma.html#pragma_synchronous) — znaczenie ustawienia FULL.

## Utrzymanie

Przy zmianie zachowania należy zaktualizować twierdzenie, dowód i dokument użytkowy w tym samym pull requeście. Jeśli dowód jest planem lub ADR o statusie Proposed, raport musi nazwać go planem, nie stanem wdrożonym.

