# Delivery Semantics Implementation Plan

## Summary

Implement RocketMQ’s confirmed delivery contract:

- At-least-once delivery.
- FIFO lease order per queue.
- Multiple consumers allowed.
- Explicit acknowledgement deadline.
- Stable message identity for idempotent consumers.
- Automatic redelivery after lease expiry.
- Automatic dead-lettering after a finite delivery limit.

Lease renewal, streaming consumption, and server-side deduplication remain out of scope for this phase.

## Core Model and Store Behavior

### Stable message identity

Extend `LeasedMessage` with the store-assigned `MessageId`:

```csharp
public sealed record LeasedMessage(
    Guid MessageId,
    Guid LeaseId,
    InboundMessage Message,
    int DeliveryCount,
    DateTimeOffset LeaseExpiresAtUtc);
```

The `MessageId` remains stable across redeliveries; every lease receives a new `LeaseId`. Consumers use `MessageId` as their idempotency key.

### Lease and FIFO rules

- `LeaseNextAsync` leases the oldest available message by a monotonically increasing enqueue sequence.
- Multiple consumers may lease different messages concurrently.
- FIFO applies to lease order, not handler completion order.
- An active lease prevents the same message from being leased again.
- An expired lease becomes eligible for redelivery when the next lease operation runs.
- Expiry is inclusive: an ack/nack at or after the deadline is rejected.
- No background timer is required; expiration can be evaluated lazily during leasing.

Use `TimeProvider` or an equivalent injectable clock in the in-memory implementation so timeout tests do not depend on arbitrary `Task.Delay` calls.

### Acknowledgement behavior

- `AckAsync` permanently removes the message only when the lease is active.
- `NackAsync(requeue: true)` makes the message available immediately and preserves its delivery count.
- `NackAsync(requeue: false)` moves the message to dead letters.
- Every redelivery receives a new lease ID and increments `DeliveryCount`.
- Unknown lease IDs and expired lease IDs must remain distinguishable internally.

### Delivery limit

- `MaxDeliveryCount = 10` is the default queue limit.
- `MaxDeliveryCount = 0` means unlimited redelivery.
- A message may be delivered at most ten times when the limit is 10.
- Before creating an 11th lease, move the message to dead letters.
- Use reason `max-delivery-count-exceeded`.
- Explicit `Nack(requeue=false)` uses reason `consumer-rejected`.

Update the in-memory queue store to enforce these rules atomically while preserving FIFO.

## gRPC and SDK Changes

### Protobuf

Add an additive `message_id` field to `LeaseResponse` without renumbering existing fields:

```protobuf
string message_id = 5;
```

Keep `visibility_timeout_seconds` as the explicit ack-deadline input.

### Validation and errors

Use these gRPC statuses:

- `INVALID_ARGUMENT`: malformed lease ID, non-positive timeout, or timeout above the configured maximum.
- `NOT_FOUND`: syntactically valid but unknown lease ID.
- `FAILED_PRECONDITION`: lease exists but is expired or no longer active.

Set timeout policy to:

- Default: 30 seconds.
- Minimum: 1 second.
- Maximum: 1 hour.
- The server validates the request before calling the store.

### .NET client

Add `MessageId` to `MessageContext`.

Add configurable consumer options:

```csharp
public sealed class ConsumerOptions
{
    public TimeSpan VisibilityTimeout { get; init; } =
        TimeSpan.FromSeconds(30);
}
```

Preserve the existing consumer overload for compatibility and add an overload accepting `ConsumerOptions`. The SDK must send the configured timeout, expose the stable message ID to handlers, and retain the current behavior of requeueing when a handler throws.

Do not add lease renewal or parallel handler execution in this phase.

## Tests

Extend the existing contract and integration coverage with:

- FIFO lease order for multiple messages.
- Concurrent leases never returning the same message.
- Stable `MessageId` across redelivery.
- New `LeaseId` for every delivery.
- Redelivery after timeout.
- Delivery count incrementing on timeout and requeue.
- Ack and nack rejected after expiry.
- Ack permanently removing a message.
- Requeue making a message immediately available.
- Explicit dead-lettering with the correct reason.
- Automatic dead-lettering on the delivery-limit boundary.
- Unlimited redelivery when `MaxDeliveryCount = 0`.
- Invalid timeout and malformed lease ID gRPC errors.
- Consumer SDK forwarding configured timeout and exposing `MessageId`.
- End-to-end idempotency example showing duplicate delivery can be detected using `MessageId`.

Use an injectable clock for deterministic timeout tests; retain one real-time integration test to verify behavior with the production clock.

## Acceptance Criteria

- The in-memory adapter passes all queue contract tests.
- `dotnet test RocketMQ.slnx` remains green after the changes.
- FIFO lease order is deterministic under concurrent callers.
- A message is never concurrently leased twice.
- A message is never delivered more than the configured maximum unless unlimited mode is selected.
- Expired acknowledgements cannot delete or requeue a later redelivery.
- The public gRPC response exposes a stable message ID.
- Existing clients remain source-compatible through the consumer overload.
- No SQLite schema or WAL implementation is introduced in this phase; the same semantics must be reused by those adapters later.

## Assumptions and Defaults

- FIFO means FIFO lease order, not strict processing completion order.
- Default ack deadline is 30 seconds.
- Valid ack deadlines are 1 second through 1 hour.
- Lease renewal is deferred.
- Server-side deduplication is not implemented; idempotency remains the consumer’s responsibility.
- `MaxDeliveryCount = 0` retains its existing unlimited meaning.
