# Repository Guidelines

## Project Structure & Module Organization

- `src/Core/RocketMQ.Core` contains domain models, ports, and routing logic; keep it independent of adapters.
- `src/Client` contains the client SDK, `src/Transport/RocketMQ.Transport.Grpc` the gRPC server and protobuf contract, and `src/Persistence` the SQLite and WAL adapters.
- `src/Runner/RocketMQ.Runner` is the executable host and local integration entry point.
- `tests/` contains architecture, contract, runner-unit, and gRPC tests. Architecture decisions are documented in `docs/adr/`.

## Build, Test, and Development Commands

Run commands from the repository root (`D:\RocketMQ\RocketMQ`):

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build --verbosity normal
dotnet run --project src/Runner/RocketMQ.Runner
```

Restore resolves NuGet dependencies; build compiles the solution; test runs the full xUnit suite; run starts the local runner. CI performs the same restore, build, and test sequence on .NET 10.

## Coding Style & Naming Conventions

Use four spaces in C# and two spaces in XML, JSON, and project files. Follow `.editorconfig`: file-scoped namespaces, braces for all blocks, `var` for locals, PascalCase for public types/members/constants, and `_camelCase` for private/internal fields. Keep nullable reference types enabled and treat analyzer/style issues as build-relevant.

## Testing Guidelines

Add or update xUnit tests alongside behavior changes. Name test classes after the subject (for example, `MessageRouterTests`) and methods to describe the expected behavior. Adapter implementations should satisfy the shared contract tests; do not weaken a contract test to accommodate an adapter. Run `dotnet test` before submitting changes.

## Commit & Pull Request Guidelines

Recent commits use short, imperative, sentence-style subjects such as `Grpc transport` and `Message router`; keep commits focused and messages similarly concise. Pull requests should explain the behavior or architecture change, identify affected projects, link relevant issues, and include the commands used to validate it. Update an ADR when changing a significant architectural decision.

## Agent-Specific Instructions

Read `CLAUDE.md` before changing architecture or persistence behavior. Preserve core/adaptor boundaries, explicit bounded-channel settings on network-to-disk paths, and the existing ADR and test conventions.
