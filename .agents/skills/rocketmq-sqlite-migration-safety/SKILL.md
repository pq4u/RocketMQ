---
name: rocketmq-sqlite-migration-safety
description: Design and verify safe RocketMQ SQLite schema and storage evolution. Use when changing SqliteDatabase initialization, tables, indexes, columns, schema_migrations, PRAGMA settings, retention, backup or restore behavior, transaction boundaries, or startup migration logic. Do not use for query-only changes that cannot affect stored data or schema compatibility.
---

# SQLite migration safety

## Inspect before changing

1. Read `docs/decisions/03-persistence-strategy.md`, `SqliteDatabase.cs`, and every adapter that reads or writes the affected objects.
2. Identify the current schema version, supported upgrade origin, data volume assumptions, and whether the change is additive or destructive.
3. Confirm that the database path is local storage and that only one broker writer is supported.

## Design the migration

- Add a new monotonically increasing migration; do not rewrite an already shipped migration to represent a new schema.
- Make migration discovery and execution deterministic and ordered.
- Execute each migration atomically where SQLite permits it.
- Record the version only after the migration succeeds.
- Fail startup on an unknown newer version, a partially applied migration, or an invariant violation.
- Require a backup before a non-additive migration and document the restore boundary.
- Preserve `journal_mode=WAL`, `synchronous=FULL`, `foreign_keys=ON`, and a finite `busy_timeout` on every connection.
- Keep migration work out of the request path and complete it before the server becomes ready.

## Verify compatibility and failure behavior

Test all applicable cases:

1. Empty database initializes to the latest schema.
2. A database at each supported older version upgrades and preserves data.
3. Reopening an upgraded database performs no duplicate work.
4. A forced migration failure does not record success or expose a partially usable broker.
5. Foreign keys, indexes, uniqueness rules, and durability PRAGMAs remain active.
6. Existing publish, lease, ack/nack, routing, and dead-letter behavior still passes its contract tests.

Use disposable copies of representative databases. Never experiment on an existing user or production database.

## Report

State the version transition, backup requirement, rollback or restore story, compatibility window, and exact validation commands. Flag any schema change that cannot be safely rolled back.
