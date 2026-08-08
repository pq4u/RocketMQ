# Decision 08: Deployment and Scaling

## Status

Open.

## Current baseline

The runner is a single .NET process using in-memory stores. The agreed first persistence backend is SQLite, which naturally supports a single authoritative broker node. There is no clustering, replication, leader election, or deployment packaging yet.

## Analysis

Single-node operation reduces complexity and is the right match for SQLite. It does not provide failover: a process or host failure interrupts service, and a single database file becomes the scaling boundary. Introducing clustering before the delivery and persistence contracts are stable would make correctness harder to validate.

The project still needs an explicit deployment contract: configuration source, storage path, port binding, logging, health checks, backup procedure, and upgrade/migration behavior. The current server hard-codes port 50051 and has no TLS configuration.

## Recommended default

Target a production-quality single-node broker first. Provide a container and a documented systemd/Windows-service option, externalize all runtime configuration, expose health/readiness checks, and define SQLite backup and migration procedures. Treat horizontal scaling and HA as a later version with a separate replication design.

## Questions

1. Is single-node deployment acceptable for the first usable release?
2. What throughput, latency, queue-count, and message-size targets must one node support?
3. Which deployment formats are required: Docker, Kubernetes, Linux service, Windows service, or all of them?
4. Where should the SQLite database live, and who owns backup/restore?
5. Is planned downtime acceptable for upgrades and migrations?
6. When should clustering/replication begin: after a benchmark threshold, customer need, or a fixed release?
7. Should multiple broker processes ever share one SQLite database? Recommended answer: no.
