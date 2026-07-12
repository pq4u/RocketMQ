# CLAUDE.md — RocketMQ Project Guide

This file provides context for AI assistants working on this codebase.

## Architecture

RocketMQ follows a **hexagonal architecture** (ports & adapters). Core
business logic lives in `src/Core/RocketMQ.Core` and depends only on
abstractions (interfaces). Adapters implement those interfaces and live
in separate projects under `src/Persistence/` and `src/Transport/`.

### Core Ports (src/Core/RocketMQ.Core/Abstractions)

| Port | Purpose | Semantics |
|------|---------|----------|
| `IMessageQueueStore` | Queue persistence with lease/ack/nack | RabbitMQ-style competing consumers with visibility timeout (see ADR-0001) |
| `IPersistenceStore` | Append-only durable log | Kafka-style offset-based replay (retained for future event-sourcing use) |
| `IMessageChannel<T>` | Backpressure boundary (bounded channel) | Producer→consumer flow control, never drops messages |
| `ITransportServer` | Network transport (gRPC, TCP/Pipelines) | Accepts connections, pushes into IMessageChannel |

### Core Domain Types

- `InboundMessage` — immutable record: the unit of data flowing through the
  system. Transport-agnostic, persistence-agnostic. Does NOT carry queue
  state (see ADR-0001).
- `LeasedMessage` — wrapper around `InboundMessage` with lease metadata
  (`LeaseId`, `DeliveryCount`, `LeaseExpiresAtUtc`). Returned by
  `IMessageQueueStore.LeaseNextAsync`.
- `DeadLetteredMessage` — wrapper for messages nack'd with `requeue=false`.
  Browseable via `IMessageQueueStore.BrowseDeadLettersAsync`.

### Adapters

| Adapter | Implements | Project |
|---------|-----------|--------|
| `SqlitePersistenceStore` | `IPersistenceStore` | `RocketMQ.Persistence.Sqlite` |
| `SqliteMessageQueueStore` | `IMessageQueueStore` | `RocketMQ.Persistence.Sqlite` |
| `CustomWalPersistenceStore` | `IPersistenceStore` | `RocketMQ.Persistence.Wal` |
| `WalMessageQueueStore` | `IMessageQueueStore` | `RocketMQ.Persistence.Wal` |

## Testing Strategy

### Contract Tests (tests/RocketMQ.Contract.Tests)

Abstract test classes that define the behavioral contract for each port.
Every adapter inherits the appropriate contract test class and only has to
implement `CreateStoreAsync()`. If two adapters both pass the same contract
tests, they are behaviorally interchangeable.

- `PersistenceStoreContractTests` — verifies `IPersistenceStore` contract
- `MessageQueueStoreContractTests` — verifies `IMessageQueueStore` contract

**Rule: Do not weaken contract tests to make an adapter pass.**

### Architecture Tests (tests/RocketMQ.Architecture.Tests)

NetArchTest-based tests that enforce dependency rules:
- Core must not depend on any adapter technology (Grpc, SQLite, Pipelines)
- Abstractions namespace must only contain interfaces
- Domain types must be sealed

## Conventions

### Channels

System.Threading.Channels usage: capacity and `FullMode` must ALWAYS be
explicit — never `Channel.CreateUnbounded` on the network→disk production
path.

### Persistence Skeletons

New adapter methods follow the pattern: `throw new NotImplementedException`
with a `TODO` comment referencing the specific contract point(s) that the
implementation must satisfy.

## ADRs

Architecture Decision Records live in `docs/adr/`.

- [ADR-0001](docs/adr/0001-queue-over-log.md) — Queue model (RabbitMQ-style)
  over log model (Kafka-style)
