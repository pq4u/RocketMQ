# Test Foundation Implementation Plan

## Summary

Make `dotnet test RocketMQ.slnx` reliable and meaningful by standardizing on xUnit v3 with the Visual Studio adapter, repairing architecture-test discovery, and executing the shared contract tests against the in-memory adapters. The result must be a green, repeatable baseline before SQLite persistence work begins.

## Implementation Changes

### Test runner and project configuration

- Standardize executable test projects on the currently working configuration:
  - `Microsoft.NET.Test.Sdk`
  - `xunit.v3`
  - `xunit.runner.visualstudio`
  - xUnit v3-compatible test output configuration.
- Add the missing `xunit.v3` package to `RocketMQ.Architecture.Tests`.
- Keep `dotnet test` plus the Visual Studio adapter as the supported execution model; do not migrate to Microsoft Testing Platform.
- Keep `RocketMQ.Contract.Tests` as a reusable fixture library containing abstract contract classes, not as a standalone test target.

### Architecture tests

- Correct the domain namespace assertion from `RocketMQ.Core.Domain` to `RocketMQ.Core.Models`.
- Ensure architecture tests are discovered and executed by `dotnet test`.
- Preserve checks that:
  - Core has no gRPC, SQLite, or pipeline dependencies.
  - `RocketMQ.Core.Abstractions` contains interfaces only.
  - Core model classes are sealed.

### Concrete contract tests

- Add concrete subclasses of:
  - `MessageQueueStoreContractTests`
  - `RoutingStoreContractTests`
- Place these subclasses in the adapter-specific test project, using the selected “adapter test projects” structure.
- Initially run the contracts against `InMemoryMessageQueueStore` and `InMemoryRoutingStore`.
- Add project references only from test projects to the contract fixture library and the adapter under test.
- Leave SQLite/WAL concrete fixtures for the persistence implementation phase, but make the fixture structure ready for them.

### In-memory implementation fixes required by the contracts

- Implement `BrowseDeadLettersAsync`.
- Make exchange and queue declarations idempotent for identical configuration and reject conflicting configuration.
- Validate exchange and queue existence before creating bindings.
- Remove bindings when exchanges or queues are deleted.
- Preserve thread safety and FIFO behavior while making these fixes.

### Integration coverage

Add an in-process broker-flow test that:

1. Declares an exchange, queue, and binding.
2. Publishes through `ProducerService`.
3. Routes the resulting envelope.
4. Leases through `ConsumerService`.
5. Acknowledges the lease.
6. Verifies the message is not delivered again.

This test should avoid a fixed network port and validate the broker pipeline through its service and core boundaries.

## Test and CI Acceptance Criteria

- `dotnet build RocketMQ.slnx` succeeds without new warnings caused by the test foundation.
- `dotnet test RocketMQ.slnx --no-build --verbosity normal` exits successfully.
- Architecture tests are reported as executed, not “no test available” or catastrophic discovery failures.
- In-memory queue and routing contract suites are reported as executed.
- Existing runner-unit and gRPC tests continue to pass.
- The integration flow test passes.
- CI uses the same `dotnet restore`, `dotnet build`, and `dotnet test` commands as local development.

## Assumptions and Defaults

- The supported runner remains `dotnet test` with xUnit v3 and the Visual Studio adapter.
- Contract fixtures remain abstract and reusable; concrete adapter tests live beside each adapter.
- This phase fixes test infrastructure and in-memory contract compliance only.
- SQLite persistence, WAL implementation, authentication, streaming consumers, and publish-confirmation redesign remain outside this phase.
