# ADR-0007: Local management REST API and web UI

- **Status:** Accepted
- **Date:** 2026-09-11
- **Deciders:** Team

## Context

The gRPC Admin service supports topology declarations needed by clients, but operators also need topology discovery, queue statistics, non-destructive message inspection, dead-letter recovery and a compact runtime overview. Extending the public protobuf contract for every operator workflow would couple the browser UI to gRPC-Web and make the operational surface harder to evolve. Direct browser access to SQLite would bypass broker invariants and is not acceptable.

## Decision

The Runner can start an opt-in management surface composed of an ASP.NET Core Minimal API under `/api/v1` and a Blazor WebAssembly client using MudBlazor. Both are served by a second in-process `WebApplication` on a port distinct from the gRPC transport. Shared request and response records live in `RocketMQ.Management.Contracts`; the browser knows only the REST contract.

The first version is deliberately local-only. `RocketMQ:Management:Enabled` defaults to `false`, and startup rejects a management URL that is not loopback. HTTP and HTTPS are accepted on loopback. There is no separate management authentication in this increment, so binding to a remote interface is forbidden rather than merely discouraged.

Core exposes a separate `IQueueManagementStore` port for inspection and administrative mutations. The SQLite adapter implements statistics, cursor-based browse, bounded payload preview, purge and dead-letter operations. This keeps delivery semantics in `IMessageQueueStore` unchanged. Ready-message inspection never creates a lease. Requeueing a dead letter clears its failure and lease fields and resets delivery count.

Operational counters are collected by decorators around the publisher and queue store. One-minute buckets are kept in process memory for up to 24 hours; they are diagnostic, reset on restart and are not a durable telemetry system. OpenAPI is exposed at `/openapi/v1.json`, and failures use Problem Details with a stable `code` extension.

## Consequences

- The panel and REST API evolve without changing protobuf or the .NET client SDK.
- Topology writes still pass through `IRoutingStore`, and message mutations pass through an explicit Core port rather than raw UI SQL.
- The management host adds another local listener and shares the broker process lifecycle and SQLite database.
- Payload preview is capped at 64 KiB, test publish at 1 MiB, and list pages at 100 items to bound memory use.
- Browser authentication, remote administration, durable metrics, audit logging, multi-node aggregation and live push updates remain outside this decision.
