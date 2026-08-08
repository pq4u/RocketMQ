# Broker Decisions

These documents capture the product and architecture decisions needed to make RocketMQ a usable RabbitMQ-inspired broker over gRPC. Each document separates the current baseline, analysis, recommendation, and questions requiring a decision.

## Confirmed direction

- [01 — Protocol and compatibility](01-protocol-and-compatibility.md): RabbitMQ-inspired semantics with gRPC as the supported transport.
- [02 — Delivery semantics](02-delivery-semantics.md): at-least-once delivery, FIFO per queue, explicit ack deadline, and idempotent consumers.
- [03 — Persistence strategy](03-persistence-strategy.md): SQLite first, then WAL optimization.

## Open decisions

- [04 — Publish confirmation](04-publish-confirmation.md)
- [05 — Topology semantics](05-topology-semantics.md)
- [06 — Consumer interaction model](06-consumer-interaction-model.md)
- [07 — Message schema](07-message-schema.md)
- [08 — Deployment and scaling](08-deployment-and-scaling.md)
- [09 — Security baseline](09-security-baseline.md)

Answer the questions in each document; the answers can then be converted into accepted ADRs and implementation tasks.
