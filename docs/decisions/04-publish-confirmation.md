# Decision 04: Publish Confirmation

## Status

Open.

## Current behavior

The gRPC producer calls `IMessagePublisher`. The SQLite publisher returns only
after a transaction records the idempotency result and every routed queue copy.
Unknown exchanges and publish-id conflicts are returned as gRPC errors;
an exchange without a matching binding returns an explicit `Unroutable`
response. The bounded channel is internal to the SQLite publisher and is used
for batching, not as the confirmation boundary.

## Analysis

Publisher confirmation is the boundary between a reliable broker and a best-effort in-process pipeline. With fanout or multiple matching bindings, one publish may affect several queues. The broker must define whether confirmation means all destinations accepted the message, some destinations accepted it, or the broker accepted responsibility for retrying the routing operation.

Unroutable messages also need an explicit result. Silently dropping them is unsafe. A client may need the message ID and a reason for rejection so it can retry or alert.

## Recommended default

Return success only after the message has been durably committed to every resolved destination queue. Return a structured result containing a stable message ID and routing outcome. Reject unknown exchanges and invalid topology. For an exchange with no matching binding, return an explicit `Unroutable` result; do not silently drop the message.

For the first implementation, route and persist synchronously through a broker service or durable outbox rather than treating the in-memory channel write as confirmation.

## Implemented semantics

- `Publish` confirms only after one SQLite transaction records its publish-idempotency entry and every routed queue copy.
- Response returns broker `message_id`, `publish_id`, routing status, and destination queues. `Unroutable` is explicit and its result is retained for idempotent retries.
- A `publish_id` is retained for 24 hours. Repeating it with identical exchange, routing key, correlation ID, and payload returns the original outcome; different content returns `AlreadyExists`.
- Unknown exchanges return gRPC `NotFound`.

## Questions

1. Should `Publish` wait for durable enqueue into all matching queues?
2. If one fanout destination fails, should the whole publish fail, retry internally, or report partial success?
3. Should unroutable messages be rejected, returned to the publisher, or sent to an alternate exchange?
4. Should the publish response contain `message_id`, destination queues, and a routing status?
5. Is an asynchronous publisher-confirm API needed later for high throughput?
