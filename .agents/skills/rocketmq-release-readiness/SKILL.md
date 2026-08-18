---
name: rocketmq-release-readiness
description: Produce a read-only, evidence-based RocketMQ pre-release readiness assessment. Use explicitly when asked for release readiness, a preflight audit, dependency and vulnerability review, CI parity, packaging checks, deployment safety, or a ship/no-ship report. Do not publish packages, create tags, commit changes, or deploy anything.
---

# Release readiness assessment

## Preserve the workspace

- Start with `git status --short` and distinguish pre-existing user changes from audit output.
- Keep the assessment read-only except for normal ignored build and test artifacts.
- Do not clean, reset, commit, tag, publish, upload, or deploy.
- Report checks that could not run because of network, credentials, tools, or environment limitations.

## Run the readiness lanes

### Build and tests

1. Compare local commands with `.github/workflows/dotnet.yml` and `AGENTS.md`.
2. Restore only when authorized or already available, then build and test `RocketMQ.slnx`.
3. Build the sibling `RocketMQ.Example.sln` when present.
4. Confirm that architecture and concrete adapter contract suites actually executed, not merely compiled.

### Dependencies and toolchain

- Report direct and transitive vulnerability warnings.
- Check outdated packages when network access is available.
- Identify inconsistent gRPC, protobuf, .NET, test-runner, and Microsoft.Extensions versions.
- Report preview SDK usage and the absence or mismatch of a pinned SDK policy.

### Product and operations

- Check bind address, TLS, authentication, authorization, payload and rate limits, health/readiness, logging, backup/restore, migrations, configuration externalization, and shutdown behavior.
- Confirm that README limitations match executable behavior.
- Check whether examples and benchmark tools are covered by CI.

### Repository hygiene

- Flag tracked or unignored local artifacts such as `.user`, `.bak`, crash logs, database files, and generated benchmark output.
- Check for stale paths, machine-specific configuration, and accidental secrets without printing secret values.

## Decide

Classify every finding as `pass`, `warning`, `fail`, or `not checked`, with evidence and remediation. Give a final `ready`, `ready with warnings`, or `not ready` decision tied to explicit blockers. Never turn the assessment into a release operation.
