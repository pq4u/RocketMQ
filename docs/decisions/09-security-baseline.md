# Decision 09: Security Baseline

## Status

Partially implemented; authorization, limits, audit, and production rollout remain open.

## Current baseline

The gRPC server defaults to HTTP/2 with TLS at `https://localhost:50051`. Kestrel loads the server certificate from its standard configuration or the local development certificate. Explicit cleartext HTTP is accepted only on loopback.

Mutual TLS is implemented as an opt-in transport setting. When enabled, every endpoint must use HTTPS and Kestrel requires a client certificate whose chain terminates at the configured private CA. The .NET SDK, example, and benchmark can load PFX/P12 or PEM client credentials. Certificate chains are checked when a connection is established and the HTTP/2 connection is reused. CA and client certificate rotation requires a process restart.

There is still no authorization, tenant boundary, quota enforcement, or audit trail. Any client with a certificate accepted by the configured CA can publish, consume, and modify topology.

## Analysis

Messaging systems carry application data and control operations, so transport encryption alone is insufficient. Administration must be separated from normal publish/consume permissions. The broker also needs limits to prevent one client from exhausting memory, queue storage, connections, or channel capacity.

Security choices affect protobuf metadata, deployment, client SDK configuration, operational secrets, and tests. They should be decided before adding public examples that use unauthenticated endpoints.

## Recommended default

For a first networked release:

- keep TLS enabled outside local development;
- enable the implemented mTLS mode and provision a private client CA when certificate-based service identities fit the target environment;
- authorize publish, consume, and topology operations separately;
- add per-client connection, payload, publish-rate, and queue-consumer limits;
- log security-sensitive administration and authentication events without logging payloads by default.

For local development, allow explicit insecure mode bound to loopback rather than `AnyIP`.

## Questions

1. Is the implemented mTLS identity sufficient for the target deployments, or are JWT/OIDC, API keys, or a pluggable provider also required?
2. Do you need users, service accounts, or both?
3. Should permissions be scoped by operation, exchange, queue, tenant, or namespace?
4. Is multi-tenancy required in the first release?
5. What connection, payload, queue, and rate limits are acceptable defaults?
6. Which audit events must be retained, and for how long?
7. When must the current restart-based certificate rotation be replaced by automatic rotation?
