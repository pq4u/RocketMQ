# Decision 01: Protocol and Compatibility

## Status

Confirmed direction; details remain open.

## Current baseline

RocketMQ is RabbitMQ-inspired, not RabbitMQ-compatible. gRPC/HTTP2 is the supported client transport. The current protobuf contract exposes `Producer`, `Consumer`, and `Admin` services in [`rocketmq.proto`](../../src/Transport/RocketMQ.Transport.Grpc/Protos/rocketmq.proto).

## Analysis

This keeps the implementation focused and gives strongly typed clients in C#, Go, Java, Python, and other gRPC ecosystems. It also means RabbitMQ clients, AMQP tooling, and AMQP concepts cannot be assumed to work. The protobuf contract becomes a public compatibility surface and needs explicit versioning, error semantics, and backward-compatibility rules.

The current API is unary-only and does not yet define a stable error model, API version policy, capability discovery, or compatibility guarantees.

## Recommended default

Use a versioned gRPC package such as `rocketmq.v1`. Treat protobuf field numbers as permanent once released, add fields instead of renumbering them, and keep server changes backward compatible within a major version. Document that RocketMQ does not implement the AMQP wire protocol.

## Questions

1. Should the first public API remain gRPC-only, or should an HTTP/JSON gateway also be supported?
2. Which client languages must have maintained SDKs beyond the current .NET client?
3. Should a breaking protocol change create `rocketmq.v2`, or should compatibility be maintained indefinitely within `v1`?
4. Do you want the broker to expose a `GetCapabilities` or version endpoint?
5. Which error categories must clients distinguish: invalid request, unavailable broker, not found, conflict, resource exhausted, and failed durability?
