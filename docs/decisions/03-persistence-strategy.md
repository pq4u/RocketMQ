# Decision 03: Persistence Strategy

## Status

Confirmed direction: SQLite first, WAL later.

## Current baseline

The runner currently registers in-memory queue and routing stores. The SQLite and custom WAL projects exist, but their implementations are incomplete and contain `NotImplementedException` paths, for example [`SqliteMessageQueueStore.cs`](../../src/Persistence/RocketMQ.Persistence.Sqlite/SqliteMessageQueueStore.cs) and [`WalMessageQueueStore.cs`](../../src/Persistence/RocketMQ.Persistence.Wal/WalMessageQueueStore.cs).

## Analysis

SQLite is a good first durable backend because it provides transactions, indexing, crash recovery, and a mature WAL mode without requiring a custom file format. The queue store and routing store must share a clear transaction model. A successful publish must not be reported before the message is durably accepted according to the publish-confirmation decision.

The schema must represent messages, queue state, leases, delivery counts, dead letters, exchanges, queues, bindings, and schema version. Lease recovery after restart must be deterministic. The custom WAL should only be introduced after measurements demonstrate that SQLite is the bottleneck.

## Recommended default

Implement one SQLite database per broker node, enable SQLite WAL mode, use explicit transactions, and add migrations. Make SQLite the only supported durable backend for the first usable release. Keep the adapter contract tests as the compatibility gate before beginning WAL optimization.

## Questions

1. Should all queues and topology share one database file, or should queues be sharded across files?
2. What durability level is required before publish confirmation: SQLite `FULL` synchronous mode, or a lower-latency setting?
3. Should non-durable queues exist, and if so, what exactly survives a restart?
4. How should a crash during lease, ack, nack, or routing recovery be resolved?
5. What message retention and dead-letter retention policies are required?
6. What benchmark threshold should justify replacing or supplementing SQLite with WAL?
