# Decision 05: Topology Semantics

## Status

Open.

## Current model

The domain contains exchanges, named queues, bindings, and three exchange types: direct, fanout, and topic. The routing-store contract describes idempotent declarations, referential integrity, and binding cleanup, but the current in-memory implementation does not fully enforce those rules.

## Analysis

Topology rules determine whether clients can safely start repeatedly, whether deployments can migrate without downtime, and how configuration conflicts are handled. RabbitMQ-like systems commonly distinguish durable, temporary, exclusive, and auto-delete entities. RocketMQ currently has only `Durable` and `MaxDeliveryCount` on queues and no delete or unbind gRPC operations.

The default exchange is described in ADR-0002 but is not created and auto-bound by the current runner. This must be settled before clients rely on simple queue-name publishing.

## Recommended default

For the first release:

- Declarations are idempotent only when configuration matches; conflicts fail.
- Bindings require existing exchange and queue and are idempotent.
- Deleting an exchange or queue removes its bindings.
- Create a durable empty-name direct exchange and auto-bind each declared queue using its name as routing key.
- Defer exclusive, auto-delete, alternate exchanges, and transactions until the base model is stable.

## Questions

1. Should queues support temporary, exclusive, and auto-delete modes now or later?
2. Should deleting a queue delete queued messages immediately?
3. Should publishing to an unknown exchange fail, or should the default exchange be used?
4. Should the default exchange be mandatory and always present?
5. Do you need alternate exchanges for unroutable messages?
6. Should queue and exchange names have length, character, and namespace restrictions?
7. Which topology operations must be exposed through gRPC: delete, unbind, list, inspect, purge, and dead-letter management?
