# gRPC Transport Guidelines

These instructions apply to the gRPC server and protobuf contract. Follow the
repository-level `AGENTS.md` and `CLAUDE.md` in addition to this file.

## Public contract evolution

- Treat `Protos/rocketmq.proto` as a public compatibility boundary.
- Prefer additive changes. Never reuse an existing protobuf field number; reserve
  removed field numbers and names.
- When the contract changes, update the server implementation, generated client
  consumers, `src/Client`, transport tests, examples, and relevant documentation
  in the same change.
- Preserve existing RPC and message names unless a deliberate breaking release is
  documented and approved.

## Service behavior

- Validate requests at the transport boundary and map domain failures to stable,
  intentional gRPC status codes. Do not leak database exceptions or internal
  implementation details to clients.
- Propagate `ServerCallContext.CancellationToken` through Core and persistence
  calls whenever the called API accepts cancellation.
- Keep business rules in Core or application services; gRPC services should map,
  validate, delegate, and translate results.
- Keep retry behavior bounded and limited to explicitly transient status codes.
- Changes to listeners, TLS, authentication, authorization, or externally visible
  endpoints require corresponding runner configuration, tests, and documentation.

## Validation

```powershell
dotnet build src/Client/RocketMQ.Client/RocketMQ.Client.csproj --no-restore
dotnet test tests/RocketMQ.Transport.Grpc.Tests/RocketMQ.Transport.Grpc.Tests.csproj --no-restore
```

Run the full solution tests when a protobuf change affects more than the transport
project.
