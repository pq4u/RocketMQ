---
name: rocketmq-grpc-api-evolution
description: Evolve the RocketMQ gRPC and protobuf API while keeping the server, .NET SDK, benchmark client, tests, and examples compatible. Use when changing rocketmq.proto, RPC methods, protobuf fields, gRPC status mapping, generated client inputs, public SDK methods, or gRPC package versions. Do not use for internal changes with no wire or SDK effect.
---

# gRPC API evolution

## Map the compatibility surface

Read:

- `src/Transport/RocketMQ.Transport.Grpc/Protos/rocketmq.proto`;
- all three gRPC services;
- `src/Client/RocketMQ.Client`;
- `tools/RocketMQ.Benchmark`;
- `RocketMQ.Example` when it is present beside the repository;
- Decision 01 and ADRs 0003 and 0004.

Identify every server, generated client, facade, test, example, and document affected by the proposed change.

## Preserve wire compatibility

- Never renumber or reuse an existing protobuf field number.
- Prefer additive optional fields and new RPCs within `rocketmq.v1`.
- Reserve removed field numbers and names.
- Preserve useful behavior for omitted fields from older clients.
- Introduce a new package version for an intentionally breaking wire change.
- Keep error semantics explicit: validate input before storage calls and map domain failures consistently to gRPC statuses.
- Forward deadlines and cancellation tokens across the boundary.
- Treat enum parsing and unknown values deliberately; do not silently convert invalid input to a valid operation.

## Keep all consumers aligned

1. Update the server implementation and validation.
2. Update the public .NET facade without exposing generated protobuf types unnecessarily.
3. Update benchmark and example clients.
4. Align gRPC runtime and tooling versions deliberately across projects.
5. Add service tests for valid input, invalid input, domain-error mapping, omitted new fields, and response population.
6. Update protocol and SDK documentation when implemented behavior changes.

## Validate

```powershell
dotnet build RocketMQ.slnx --no-restore
dotnet test RocketMQ.slnx --no-build --verbosity normal
dotnet build ..\RocketMQ.Example\RocketMQ.Example.sln --no-restore
```

Skip the example command only when that sibling workspace is absent, and say so. Summarize wire compatibility, source compatibility, and any required client migration separately.
