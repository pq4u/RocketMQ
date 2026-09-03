# 3. gRPC Transport Layer and API Contract

Date: 2026-07-12

## Status

Proposed

> **Implementation note (2026-09-02):** the three unary gRPC services and the
> protobuf contract are implemented. Runner listens on port 50051 over cleartext
> HTTP/2. ProducerService calls the durable IMessagePublisher directly. A full
> internal buffer is not currently mapped to RESOURCE_EXHAUSTED, so the
> backpressure behavior proposed below is not the current wire behavior.

## Context

With the core domain, queue semantics (ADR-0001), and routing architecture (ADR-0002) designed, we need a way for external applications (Producers and Consumers) to interact with the RocketMQ broker over a network. 

The network layer must support:
1. **Publishing**: Sending a payload with routing metadata (Exchange, RoutingKey).
2. **Consuming**: Leasing messages from a named queue with a visibility timeout, and subsequently acknowledging (Ack) or rejecting (Nack) them.
3. **Administration**: Declaring exchanges, queues, and bindings.
4. **Flow Control / Backpressure**: The broker must be able to push back on clients if its internal buffers (`IMessageChannel`) are full.

## Decision

We will use **gRPC (HTTP/2)** as our primary network transport protocol. We will define three distinct gRPC services using Protocol Buffers (`.proto`).

### 1. Protobuf Services

```protobuf
syntax = "proto3";
package rocketmq.v1;

// --- 1. Producer Service ---
service Producer {
  // Publishes a single message to an exchange
  rpc Publish (PublishRequest) returns (PublishResponse);
  
  // Future: rpc PublishStream (stream PublishRequest) returns (stream PublishResponse);
}

message PublishRequest {
  string exchange_name = 1;
  string routing_key = 2;
  bytes payload = 3;
  // CorrelationId can be extracted from metadata/headers or added here
  string correlation_id = 4;
}

message PublishResponse {
  bool success = 1;
}

// --- 2. Consumer Service ---
service Consumer {
  // Attempts to lease the next available message
  rpc LeaseNext (LeaseRequest) returns (LeaseResponse);
  
  // Acknowledges successful processing
  rpc Ack (AckRequest) returns (AckResponse);
  
  // Rejects the message (optionally requeuing)
  rpc Nack (NackRequest) returns (AckResponse);
}

message LeaseRequest {
  string queue_name = 1;
  int32 visibility_timeout_seconds = 2;
}

message LeaseResponse {
  // Empty if no messages available
  string lease_id = 1;
  bytes payload = 2;
  int32 delivery_count = 3;
  string correlation_id = 4;
}

message AckRequest {
  string lease_id = 1;
}

message NackRequest {
  string lease_id = 1;
  bool requeue = 2;
}
message AckResponse {}

// --- 3. Admin Service ---
service Admin {
  rpc DeclareExchange (DeclareExchangeRequest) returns (AdminResponse);
  rpc DeclareQueue (DeclareQueueRequest) returns (AdminResponse);
  rpc Bind (BindRequest) returns (AdminResponse);
}
// (Message definitions omitted for brevity)
```

### 2. Interaction with Hexagonal Ports

- The `RocketMQ.Transport.Grpc` project will implement these services.
- **Publish**: The `Producer.Publish` endpoint will instantiate an `InboundMessage` and wrap it in an `Envelope`. It will then write this Envelope to the `IMessageChannel<Envelope>`.
- **Lease/Ack/Nack**: The `Consumer` service endpoints will directly call the injected `IMessageQueueStore` to retrieve, ack, or nack messages.
- **Admin**: The `Admin` service will directly call the `IRoutingStore` to manage metadata.

### 3. Backpressure Propagation

When a producer calls `Publish`, the gRPC server attempts to write to the `IMessageChannel<Envelope>`. Because this channel is bounded (see CLAUDE.md), it may be full.
If the channel rejects the message, the gRPC endpoint will immediately return a **gRPC `RESOURCE_EXHAUSTED` (Status Code 8)** error. This propagates the internal backpressure over the network, signaling the client producer to slow down and retry later.

## Consequences

### Positive
- **Strong Typing:** Protobuf provides a strict contract, eliminating ambiguity about payload formats.
- **Code Generation:** Clients in C#, Java, Go, Python, etc., can be auto-generated.
- **Performance:** Binary serialization and HTTP/2 multiplexing are extremely efficient.
- **Backpressure:** gRPC's standard status codes (`RESOURCE_EXHAUSTED`) map perfectly to our bounded channel design.

### Negative
- **Load Balancing:** Requires HTTP/2-aware L7 proxies (like Envoy or HAProxy). Standard TCP load balancers will not distribute requests across connections cleanly due to HTTP/2 multiplexing.
- **Browser Clients:** gRPC requires gRPC-Web or a REST gateway to be usable directly from a web browser.
