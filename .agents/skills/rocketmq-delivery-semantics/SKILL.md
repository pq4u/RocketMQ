---
name: rocketmq-delivery-semantics
description: Protect RocketMQ at-least-once delivery invariants across Core, stores, gRPC, and the client SDK. Use when changing lease, ack, nack, visibility timeout, FIFO ordering, redelivery, MessageId or LeaseId, DeliveryCount, dead letters, MaxDeliveryCount, consumer shutdown, or related tests. Do not use for topology-only or documentation-only work.
---

# Delivery semantics

## Load the invariant set

Read `IMessageQueueStore`, Decision 02, plan 02, the affected store implementations, `ConsumerService`, and the SDK consumer before editing.

Preserve these invariants unless the user explicitly approves a new architecture decision:

- Delivery is at least once; consumers must be able to detect duplicates.
- `MessageId` remains stable across redelivery; every delivery receives a fresh `LeaseId`.
- A message cannot have two active leases concurrently.
- FIFO means lease order among available messages, not handler completion order.
- An ack or nack at or after the deadline is rejected as expired.
- `DeliveryCount` starts at one and increases exactly once per lease.
- `Nack(requeue: true)` makes the message immediately available without resetting its count.
- `Nack(requeue: false)` dead-letters with reason `consumer-rejected`.
- A finite maximum dead-letters before creating a delivery beyond the limit, using `max-delivery-count-exceeded`.
- `MaxDeliveryCount = 0` means unlimited redelivery.
- Unknown and expired leases remain distinguishable at the domain boundary and map consistently at gRPC.

## Implement atomically

- Evaluate eligibility, expiration, delivery limit, and lease creation in one atomic store operation.
- Make ack/nack validate the currently active lease, so an old lease cannot affect a later delivery.
- Forward cancellation tokens.
- Preserve behavior across in-memory and SQLite implementations through shared contracts.

## Test deterministically

- Use `TimeProvider` or an equivalent injectable clock for timeout boundary tests.
- Avoid arbitrary sleeps in unit and contract tests; retain at most one clearly labeled real-time integration test.
- Test the exact deadline, just before it, and just after it.
- Test concurrent leasing, requeue, automatic redelivery, stable IDs, delivery-limit boundaries, and graceful consumer cancellation.
- Run the shared queue-store contracts plus gRPC and SDK tests affected by the change.

Report the invariant matrix across each implementation and layer. Call out any layer that lacks coverage.
