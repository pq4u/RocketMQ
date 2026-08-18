---
name: rocketmq-routing-property-tests
description: Strengthen RocketMQ routing correctness with table-driven, property, fuzz, and adversarial tests. Use when changing TopicMatcher, MessageRouter, ExchangeType, bindings, routing-store behavior, default-exchange behavior, queue deduplication, routing-key validation, or routing performance. Do not use for delivery-store or transport changes unrelated to routing.
---

# Routing property tests

## Establish routing semantics

Read ADR-0002, `TopicMatcher`, `MessageRouter`, `IRoutingStore`, existing routing tests, and the relevant topology decision. Preserve current case sensitivity and empty-value behavior unless a decision explicitly changes them.

Cover these invariants:

- Direct exchanges match only the exact routing key.
- Fanout exchanges ignore the routing key and return every bound queue once.
- Topic `*` consumes exactly one dot-separated word.
- Topic `#` consumes zero or more words.
- Multiple matching bindings never duplicate a destination queue.
- An existing exchange with no match returns an empty result.
- A missing exchange fails with the documented domain error.
- Binding creation preserves referential integrity and idempotency.

## Build robust cases

- Add compact table-driven examples for known patterns and boundary cases.
- Add deterministic property tests with a recorded seed when generation provides value.
- Compare optimized or refactored matching against a small independent reference matcher.
- Exercise empty patterns, empty keys, adjacent separators, repeated wildcards, long keys, long patterns, and adversarial `#` placement.
- Define whether malformed patterns are rejected or treated literally before encoding tests.
- Include a bounded performance regression case for inputs that could cause excessive recursion or allocation; avoid flaky wall-clock assertions.

## Verify across layers

Run focused Core routing tests, routing-store contracts for every concrete adapter, and publish integration tests for direct, fanout, topic, unroutable, and deduplicated destinations. Keep property failures reproducible and print the seed or minimized case.

Report which semantics were preserved, which edge cases were newly specified, and whether any behavior still lacks an architectural decision.
