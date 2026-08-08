# Decision 07: Message Schema

## Status

Open.

## Current model

`InboundMessage` currently contains connection ID, correlation ID, raw payload, and receive timestamp. The protobuf request carries exchange name, routing key, payload bytes, and correlation ID. There are no headers, content type, TTL, priority, or stable broker message ID in the public API.

## Analysis

Raw bytes keep the broker language-neutral, but applications need metadata for tracing, serialization, retries, routing context, and request/reply patterns. Adding fields later is possible with protobuf, but message persistence and dead-letter inspection should use one canonical envelope from the beginning.

Not every RabbitMQ property needs to be implemented immediately. TTL and expiration, for example, affect storage indexes and queue delivery rules. Priority affects FIFO guarantees and should not be added without a clear ordering definition.

## Recommended default

Define a stable message envelope containing:

- broker-assigned `message_id`;
- optional `correlation_id` and `reply_to`;
- exchange and routing key;
- payload bytes;
- content type and encoding;
- string or byte-valued application headers;
- published and expiration timestamps;
- delivery count and dead-letter reason when inspected.

Defer priority, transactions, and arbitrary header types until required by a concrete use case. Set a maximum payload size and document it.

## Questions

1. Which properties are required for the first application: content type, headers, reply-to, message ID, TTL, or priority?
2. Should headers be `map<string,string>`, bytes, or a typed protobuf map?
3. Should the broker preserve the publisher timestamp or assign its own publish timestamp?
4. Is message size limited globally, per queue, or per tenant?
5. Should TTL expiration move messages to dead letters or silently remove them?
6. Must correlation IDs accept arbitrary strings, or remain UUIDs?
