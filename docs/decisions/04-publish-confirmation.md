# Decision 04: Publish Confirmation

## Status

Open.

## Current behavior

The gRPC producer returns success after writing an `Envelope` to the bounded channel. Routing happens later in [`RoutingWorkerService`](../../src/Runner/RocketMQ.Runner/Program.cs), and routing errors are logged rather than returned to the producer. Therefore current `success=true` does not guarantee routing, queue acceptance, or durability.

## Analysis

Publisher confirmation is the boundary between a reliable broker and a best-effort in-process pipeline. With fanout or multiple matching bindings, one publish may affect several queues. The broker must define whether confirmation means all destinations accepted the message, some destinations accepted it, or the broker accepted responsibility for retrying the routing operation.

Unroutable messages also need an explicit result. Silently dropping them is unsafe. A client may need the message ID and a reason for rejection so it can retry or alert.

## Recommended default

Return success only after the message has been durably committed to every resolved destination queue. Return a structured result containing a stable message ID and routing outcome. Reject unknown exchanges and invalid topology. For an exchange with no matching binding, return an explicit `Unroutable` result; do not silently drop the message.

For the first implementation, route and persist synchronously through a broker service or durable outbox rather than treating the in-memory channel write as confirmation.

## Questions

1. Should `Publish` wait for durable enqueue into all matching queues?
2. If one fanout destination fails, should the whole publish fail, retry internally, or report partial success?
3. Should unroutable messages be rejected, returned to the publisher, or sent to an alternate exchange?
4. Should the publish response contain `message_id`, destination queues, and a routing status?
5. Is an asynchronous publisher-confirm API needed later for high throughput?
