---
name: rocketmq-docs-adr-sync
description: Reconcile RocketMQ documentation and architecture records with implemented behavior. Use when code changes affect README, getting-started guidance, architecture diagrams, CLAUDE.md, ADRs, open decisions, implementation plans, protobuf documentation, benchmark guidance, or examples, and when auditing documentation drift. Do not use to make an unapproved architecture decision on the user's behalf.
---

# Documentation and ADR synchronization

## Establish facts and intent

1. Inspect the current diff and executable code before editing prose.
2. Read relevant tests because they often encode the implemented behavioral contract.
3. Read the related accepted ADRs and open decisions.
4. Separate factual drift from an unresolved design conflict.

Use this authority model:

- Tests and executable configuration establish current behavior.
- Accepted ADRs establish intended architectural decisions.
- Open decisions describe proposals, not facts.
- README, getting-started, examples, and architecture overviews must describe what users can run now.

If implemented behavior contradicts an accepted ADR, do not rewrite history silently. Report the conflict and create or request a superseding decision when authorized.

## Reconcile the documentation set

Check every affected surface:

- `README.md` and `docs/getting-started.md`;
- `docs/architecture.md`;
- `AGENTS.md` and `CLAUDE.md` when agent guidance changed;
- `docs/adr` and `docs/decisions`;
- `docs/plans` statuses and acceptance criteria;
- protobuf comments and SDK examples;
- `tools/RocketMQ.Benchmark/README.md`;
- the sibling `RocketMQ.Example` workspace when present.

Remove stale class names, paths, ports, defaults, limitations, and claims about unimplemented behavior. Keep historical ADR context intact and mark supersession explicitly.

## Verify

- Search the repository for replaced terminology and removed symbols.
- Validate relative links and eliminate machine-local `file:///` links.
- Build examples when code snippets mirror compilable public SDK usage.
- Run the proportionate build or tests when documentation changes configuration or commands.

Finish with a short list of corrected drift and unresolved decisions. Do not claim an open decision is accepted without recorded approval.
