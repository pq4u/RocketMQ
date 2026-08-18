# Persistence Adapter Guidelines

These instructions apply to both the SQLite and WAL persistence adapters. Follow
the repository-level `AGENTS.md` and `CLAUDE.md` in addition to this file.

## Boundaries and contracts

- Implement ports defined by `RocketMQ.Core`; do not expose SQLite, WAL, or other
  adapter-specific types through Core abstractions.
- Preserve the shared persistence, routing, and message-queue contracts. Put
  behavior common to every adapter in `tests/RocketMQ.Contract.Tests` rather than
  duplicating it in adapter-specific tests.
- Treat publish idempotency, route resolution, message insertion, and publication
  result recording as one atomic operation when they belong to one publish call.
- Propagate cancellation tokens through database and file operations.

## SQLite and schema changes

- Execute related writes in an explicit transaction and attach every command in
  that operation to the same transaction.
- Add a new ordered migration for schema changes. Never silently rewrite an
  already released migration or rely on a destructive database recreation.
- Keep foreign keys, WAL journaling, full synchronous durability, and the busy
  timeout explicit unless an ADR intentionally changes those guarantees.
- Store timestamps as normalized UTC values using invariant, round-trip-safe
  formatting. Isolate time acquisition when behavior needs deterministic tests.
- Parameterize SQL values; never compose user-controlled identifiers or values
  directly into SQL text.

## WAL adapter

- Preserve the public store contracts before optimizing layout or throughput.
- Document recovery, torn-write, checksum, and fsync assumptions when changing
  the record format or durability boundary.
- Run the benchmark gate only after correctness and recovery tests pass; do not
  weaken functional behavior to meet a throughput target.

## Validation

Run the narrowest relevant checks first, then the full suite for cross-adapter or
schema changes:

```powershell
dotnet test tests/RocketMQ.Contract.Tests/RocketMQ.Contract.Tests.csproj
dotnet test RocketMQ.slnx --no-restore --verbosity normal
```
