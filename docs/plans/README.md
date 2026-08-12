# Implementation Plans

This catalog contains implementation-ready engineering plans for RocketMQ. Product and architecture questions remain in [`docs/decisions/`](../decisions/); this directory describes how approved work should be implemented.

## Plans

| Plan | Status | Description |
| --- | --- | --- |
| [01 — Test foundation](01-test-foundation-implementation-plan.md) | Planned | Make `dotnet test` reliable and execute adapter contract tests. |
| [02 — Delivery semantics](02-delivery-semantics-implementation-plan.md) | Completed | Implement at-least-once delivery, FIFO lease order, ack deadlines, redelivery, and dead-letter limits. |

Plans are numbered in implementation order. Update the status as work moves from `Planned` to `In progress` and `Completed`.
