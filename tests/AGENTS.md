# Test Suite Guidelines

These instructions apply to every project under `tests/`. Follow the
repository-level `AGENTS.md` in addition to this file.

## Test design

- Treat shared contract tests as the source of truth for adapter behavior. Add a
  contract case for behavior every implementation must provide; do not weaken a
  contract to accommodate one adapter.
- Keep tests deterministic and independent. Use unique temporary database or file
  paths, clean up owned resources, and do not depend on test execution order.
- Avoid arbitrary sleeps and wall-clock assertions. Inject or bound time where
  possible, and assert eventual behavior with an explicit timeout when necessary.
- Test observable behavior rather than private implementation details. Include
  cancellation, idempotency, invalid input, and failure-path coverage where they
  are part of the public contract.
- Keep architecture tests aligned with the intended Core-to-adapter dependency
  direction; do not add exceptions merely to make a violating dependency pass.

## Scope and performance

- Unit and contract tests must stay fast enough for normal development. Keep load,
  soak, and throughput scenarios in benchmark projects.
- Do not lower benchmark thresholds or loosen assertions without measured evidence
  and an explanation in the change description or ADR.
- When fixing a defect, add a regression test that fails for the original behavior
  before changing the implementation.

## Validation

Run the affected test project while iterating. Before completing a cross-cutting
change, run:

```powershell
dotnet test RocketMQ.slnx --no-restore --verbosity normal
```
