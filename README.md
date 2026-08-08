# RocketMQ

RocketMQ is a RabbitMQ-inspired message broker written in C# for .NET 10. It provides named queues, exchange-based routing, competing consumers, and explicit message acknowledgement over gRPC/HTTP2.

> **Project status:** active prototype. The sample runner uses in-memory stores. The SQLite and WAL projects define persistence adapters, but their methods are still scaffolding and must not be treated as durable production storage yet.

## What it provides

- RabbitMQ-style `lease → ack/nack` delivery with visibility timeouts and at-least-once redelivery.
- Direct, fanout, and topic exchanges with `*` and `#` routing patterns.
- A bounded producer-to-router channel that reports backpressure as gRPC `RESOURCE_EXHAUSTED`.
- Unary gRPC services for publishing, consuming, and topology administration.
- A .NET client SDK with publish retries and a background consumer loop.

## Quick start

Prerequisite: .NET 10 SDK.

```powershell
dotnet restore
dotnet build --no-restore
dotnet run --project src/Runner/RocketMQ.Runner
```

The runner listens on `http://localhost:50051` using HTTP/2. It starts an in-memory queue store, routing store, router, bounded channel, and gRPC server. Stop it with `Ctrl+C`.

## Message flow

1. Declare an exchange and queue, then bind the queue with a routing key.
2. Publish a payload to the exchange.
3. The router resolves matching bindings and enqueues one copy per target queue.
4. A consumer leases a message for a visibility timeout.
5. The consumer returns `Ack` on success, or `Nack` with `requeue=true` / `false`.

See [`docs/getting-started.md`](docs/getting-started.md) for an SDK example, [`docs/architecture.md`](docs/architecture.md) for internals, and [`docs/adr/`](docs/adr/) for design decisions. The protobuf contract is in [`src/Transport/RocketMQ.Transport.Grpc/Protos/rocketmq.proto`](src/Transport/RocketMQ.Transport.Grpc/Protos/rocketmq.proto).

## Repository layout

`src/Core` contains domain types and ports; `src/Transport` contains gRPC; `src/Client` contains the SDK; `src/Persistence` contains SQLite/WAL adapters; `src/Runner` contains the local host; `tests` contains architecture, contract, unit, and gRPC tests.

## Validate changes

```powershell
dotnet test --verbosity normal
```

Keep the core independent of transport and storage technologies, and update an ADR when changing a significant broker invariant.
