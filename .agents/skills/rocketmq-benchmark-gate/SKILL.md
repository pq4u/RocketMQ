---
name: rocketmq-benchmark-gate
description: Run and assess the controlled RocketMQ persistence benchmark gate for SQLite versus a possible custom WAL. Use explicitly when asked to benchmark publish throughput or latency, compare benchmark artifacts, profile the SQLite writer path, or evaluate the Decision 03 WAL gate. Do not trigger for ordinary unit performance questions or make a backend decision from a single run.
---

# Persistence benchmark gate

## Protect the experiment

1. Read `docs/decisions/03-persistence-strategy.md` and `tools/RocketMQ.Benchmark/README.md`.
2. Record runtime, commit, OS, CPU, storage device, broker configuration, payload, routing mode, queue count, and worker count.
3. Use a dedicated fresh local database for every measured run. Never benchmark against, overwrite, or delete an existing user or production database.
4. Keep broker and benchmark versions identical across comparisons.
5. Confirm enough free disk space before starting.

## Execute comparable runs

- Use the documented warm-up and measurement durations unless the user requests an exploratory run.
- Perform at least three identical measured runs for a gate decision.
- Run direct and fanout scenarios separately.
- Do not add client retries to the benchmark path.
- Treat cancellation used to end a timed worker as shutdown bookkeeping, not automatically as a broker failure.
- Capture database, WAL, and SHM sizes and collect broker CPU and memory with a system profiler.
- Preserve raw JSON artifacts; do not rewrite measurements after the run.

## Evaluate the Decision 03 gate

Recommend a custom-WAL feasibility project only when all documented conditions hold:

1. Three supported-storage runs cannot sustain 5,000 durable publishes per second for 1 KiB messages.
2. Publish-confirmation p99 exceeds 50 ms.
3. Profiling attributes at least 60% of publish latency to SQLite commit or writer-lock contention.
4. Correctness and durability settings remained enabled throughout the test.

Do not infer causation from throughput and latency alone. Distinguish exploratory results from gate-quality evidence.

## Report

Provide a comparison table with median throughput, p50/p95/p99/max, failures by category, storage growth, environment differences, and profiler attribution. End with `gate met`, `gate not met`, or `insufficient evidence`, plus the missing evidence.
