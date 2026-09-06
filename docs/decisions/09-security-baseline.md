# Decision 09: Security Baseline

## Status

Open; required before exposing the broker outside a trusted local network.

## Current baseline

The gRPC server defaults to HTTP/2 with TLS at `https://localhost:50051`. Kestrel loads the server certificate from its standard configuration or the local development certificate. Explicit cleartext HTTP is accepted only on loopback. There is still no client authentication, authorization, tenant boundary, quota enforcement, or audit trail. Any client that can reach and trust the endpoint can publish, consume, and modify topology.

## Analysis

Messaging systems carry application data and control operations, so transport encryption alone is insufficient. Administration must be separated from normal publish/consume permissions. The broker also needs limits to prevent one client from exhausting memory, queue storage, connections, or channel capacity.

Security choices affect protobuf metadata, deployment, client SDK configuration, operational secrets, and tests. They should be decided before adding public examples that use unauthenticated endpoints.

## Recommended default

For a first networked release:

- keep TLS enabled outside local development;
- authenticate clients with an implementation that fits the target environment, such as JWT/OIDC or mTLS;
- authorize publish, consume, and topology operations separately;
- add per-client connection, payload, publish-rate, and queue-consumer limits;
- log security-sensitive administration and authentication events without logging payloads by default.

For local development, allow explicit insecure mode bound to loopback rather than `AnyIP`.

## Questions

1. Which identity system must be supported: JWT/OIDC, mTLS certificates, API keys, or a pluggable provider?
2. Do you need users, service accounts, or both?
3. Should permissions be scoped by operation, exchange, queue, tenant, or namespace?
4. Is multi-tenancy required in the first release?
5. What connection, payload, queue, and rate limits are acceptable defaults?
6. Which audit events must be retained, and for how long?
7. Should the SDK support automatic certificate or token rotation?
