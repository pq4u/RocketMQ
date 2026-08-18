# RocketMQ Benchmark

Measures completed, durable gRPC `Publish` operations against an already-running broker. It creates a unique exchange and queue topology for each run, does not consume messages, and never retries a failed RPC.

## Run

Start a broker with a dedicated, fresh SQLite database, then run. The database file itself may be absent initially; the benchmark creates its topology first and then snapshots storage:

```powershell
dotnet run --project tools/RocketMQ.Benchmark -- `
  --endpoint http://localhost:50051 `
  --database-path D:\RocketMQData\rocketmq.db
```

Defaults are a 30-second warm-up and 15-minute direct scenario with one queue, 32 closed-loop workers, and 1 KiB payloads. Reports are written to `artifacts/benchmarks/<run-id>.json`.

To include an opt-in server-side timing breakdown in the JSON report, run both
the broker and benchmark from the same build and add:

```powershell
dotnet run --project tools/RocketMQ.Benchmark -- `
  --endpoint http://localhost:50051 `
  --database-path D:\RocketMQData\rocketmq.db `
  --detailed-timings true
```

Detailed timings report distributions for writer-gate wait, connection open,
transaction begin/work/commit, publication cleanup, fingerprinting, idempotency
lookup, exchange lookup, routing, publication insert, queue inserts, and the
remaining client/transport time. Instrumentation is disabled by default so the
standard baseline remains comparable with older runs.

For a fanout scenario:

```powershell
dotnet run --project tools/RocketMQ.Benchmark -- `
  --endpoint http://localhost:50051 `
  --database-path D:\RocketMQData\rocketmq.db `
  --routing fanout `
  --queue-count 3
```

Run each comparison on a fresh database. Perform three identical direct runs before evaluating the SQLite/WAL decision. The report captures database, WAL, and SHM file sizes plus free disk space; collect broker CPU and memory separately with a system profiler.

