# RocketMQ Benchmark

Measures completed, durable gRPC `Publish` operations against an already-running broker. It creates a unique exchange and queue topology for each run, does not consume messages, and never retries a failed RPC.

## Run

Start a broker with a dedicated, fresh SQLite database, then run. The database file itself may be absent initially; the benchmark creates its topology first and then snapshots storage:

```powershell
dotnet run --project tools/RocketMQ.Benchmark -- `
  --endpoint https://localhost:50051 `
  --database-path D:\RocketMQData\rocketmq.db
```

Defaults are a 30-second warm-up and 15-minute direct scenario with one queue, 32 closed-loop workers, and 1 KiB payloads. Reports are written to `artifacts/benchmarks/<run-id>.json`.

To include an opt-in server-side timing breakdown in the JSON report, run both
the broker and benchmark from the same build and add:

```powershell
dotnet run --project tools/RocketMQ.Benchmark -- `
  --endpoint https://localhost:50051 `
  --database-path D:\RocketMQData\rocketmq.db `
  --detailed-timings true
```

Detailed timings report distributions for writer-gate wait, connection open,
transaction begin/work/commit, publication cleanup, fingerprinting, idempotency
lookup, exchange lookup, routing, publication insert, queue inserts, and the
remaining client/transport time. They also report the effective publish batch
size and time spent assembling a batch. Instrumentation is disabled by default
so the standard baseline remains comparable with older runs.

The broker groups durable publishes into a single SQLite transaction. By
default it commits when either 32 publications have arrived or 1 ms has elapsed
since the first publication in the batch. The limits can be overridden in the
broker configuration with `RocketMQ:Persistence:PublishBatchSize` and
`RocketMQ:Persistence:PublishBatchDelay`.

For a fanout scenario:

```powershell
dotnet run --project tools/RocketMQ.Benchmark -- `
  --endpoint https://localhost:50051 `
  --database-path D:\RocketMQData\rocketmq.db `
  --routing fanout `
  --queue-count 3
```

Run each comparison on a fresh database. Perform three identical direct runs before evaluating the SQLite/WAL decision. The report captures database, WAL, and SHM file sizes plus free disk space; collect broker CPU and memory separately with a system profiler.

The HTTPS certificate must be trusted by the operating system running the benchmark.

If the broker requires mTLS, supply a client certificate. The password is read
from an environment variable so it isn't exposed in process arguments:

```powershell
$env:ROCKETMQ_CLIENT_CERTIFICATE_PASSWORD = "<secret>"
dotnet run --project tools/RocketMQ.Benchmark -- `
  --endpoint https://localhost:50051 `
  --database-path D:\RocketMQData\rocketmq.db `
  --client-certificate-path D:\certs\client.pfx `
  --client-certificate-password-env ROCKETMQ_CLIENT_CERTIFICATE_PASSWORD
```

Use `--client-certificate-key-path` when the client certificate is PEM. The
certificate is loaded once and attached to the benchmark's single reusable
HTTP/2 channel. The JSON scenario records whether mTLS was enabled.

When operation authorization is enabled, register the certificate fingerprint
with global Admin and Publish permissions. The benchmark generates unique
exchange and queue names for every run, so a static exact-name ACL is not a
practical substitute unless those generated names are known and configured in
advance. The benchmark creates its topology before it starts publishing.

