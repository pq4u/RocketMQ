---
name: rocketmq-contract-first-adapter
description: Implement and review RocketMQ persistence adapters against shared behavioral contracts. Use when changing IMessageQueueStore, IRoutingStore, IPersistenceStore, SQLite or WAL adapters, persistence transactions, durability behavior, or adapter-specific contract fixtures. Do not use for transport-only, SDK-only, or documentation-only changes.
---

# Contract-first adapter work

## Establish the contract

1. Read `AGENTS.md`, `CLAUDE.md`, the affected interface under `src/Core/RocketMQ.Core/Abstractions`, and the relevant decision or ADR.
2. Inspect the working tree and preserve unrelated user changes.
3. List the exact contract points affected before editing: durability, atomicity, ordering, lease validity, idempotency, referential integrity, cancellation, or concurrency.
4. Treat Core abstractions and shared contract tests as the behavioral source of truth. If they disagree with an accepted decision, stop and report the conflict rather than silently choosing one.

## Implement through the contract

- Keep adapter dependencies out of `RocketMQ.Core`.
- Make each state transition atomic at the storage boundary.
- Return only after the documented durability boundary has completed.
- Preserve stable message identity and adapter-independent exception behavior.
- Forward cancellation tokens to every asynchronous storage operation.
- Avoid changing public contracts merely to simplify one adapter.
- Never weaken, delete, skip, or special-case a shared contract test to make an adapter pass.

## Add adapter coverage

1. Add or update a concrete fixture deriving from each applicable abstract suite in `tests/RocketMQ.Contract.Tests`.
2. Give every test an isolated store. For file-backed adapters, use a unique temporary directory and clean it through the test lifecycle.
3. Cover restart or fresh-connection visibility where durability is claimed.
4. Add focused tests only for behavior unique to the adapter; keep common behavior in shared contracts.
5. Exercise concurrent calls when the changed operation has a concurrency guarantee.

## Validate

Run the narrowest affected test project first, then:

```powershell
dotnet build RocketMQ.slnx --no-restore
dotnet test RocketMQ.slnx --no-build --verbosity normal
```

Report which contract suites ran for which adapters. Do not claim parity when an adapter lacks a concrete fixture.
