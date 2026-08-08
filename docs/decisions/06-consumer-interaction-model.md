# Decision 06: Consumer Interaction Model

## Status

Open.

## Current behavior

The gRPC API exposes unary `LeaseNext`, `Ack`, and `Nack` calls. The .NET SDK polls once per second when no message is available, uses a fixed 30-second lease, and processes one message at a time per consumer instance.

## Analysis

Unary polling is simple, language-neutral, and suitable for a first implementation, but it creates idle request traffic and limits throughput. A streaming API can reduce latency and support server-side delivery, but it complicates flow control, reconnects, lease ownership, and backpressure. Explicit leasing is useful because it preserves consumer control and makes at-least-once behavior clear.

The SDK currently has no configurable prefetch, concurrency, lease renewal, retry policy for ack/nack, or consumer identity. These choices affect FIFO behavior and recovery after client failure.

## Recommended default

Keep unary leasing for the first durable release, but make lease timeout, poll delay, prefetch, and handler concurrency configurable. Add a batch or long-poll option before introducing server streaming. Preserve explicit ack/nack semantics and make consumer shutdown stop new leases while allowing in-flight handlers to finish until a timeout.

## Questions

1. Should the first public consumer API remain polling, or is server streaming required for the initial release?
2. How many messages may one consumer lease ahead of processing?
3. Should handlers run sequentially by default, or should configurable concurrency be enabled immediately?
4. Should lease renewal be part of the first consumer contract?
5. What should happen to in-flight leases during graceful broker or consumer shutdown?
6. Should the broker identify consumers and expose consumer connection/status information?
