# Decision 02: Delivery Semantics

## Status

Confirmed direction; operational details remain open.

## Confirmed requirements

- At-least-once delivery.
- FIFO ordering per queue.
- Explicit acknowledgement deadline.
- Consumers must be idempotent.

The core interfaces already model leasing, acknowledgement, negative acknowledgement, visibility timeouts, redelivery counts, and dead letters in [`IMessageQueueStore`](../../src/Core/RocketMQ.Core/Abstractions/IMessageQueueStore.cs).

## Analysis

At-least-once delivery means a message may be delivered more than once after a crash, lost connection, expired lease, or uncertain acknowledgement result. FIFO must be defined carefully: strict FIFO is easy with one active consumer but conflicts with parallel consumers and slow messages. A queue can preserve enqueue order while allowing later messages to be leased only if the product accepts parallel processing.

The current model has no lease renewal, and `QueueDefinition.MaxDeliveryCount` is not enforced by the in-memory store. Ack and nack operations also need clear behavior when the lease has expired.

## Recommended default

For the first durable implementation, define FIFO as enqueue order for available messages, not strict completion order. Permit multiple consumers, but never lease the same message concurrently. Use a configurable visibility timeout, reject late ack/nack calls, and dead-letter after a configured maximum delivery count. Consumers should use an idempotency key based on a stable message ID.

## Questions

1. Is FIFO required to be strict across multiple concurrent consumers, or is enqueue-order leasing sufficient?
2. What should the default ack deadline be, and what minimum/maximum values are valid?
3. Should consumers be able to renew or extend a lease?
4. What happens when an ack arrives after lease expiry: return an error, or allow an idempotent late ack?
5. Does `MaxDeliveryCount = 0` mean unlimited redelivery, or should every queue have a finite safety limit?
6. Should automatic dead-lettering use a standard reason such as `max-delivery-count-exceeded`?
