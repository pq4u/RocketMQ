# ADR-0005: Certificate-based operation authorization

- **Status:** Accepted
- **Date:** 2026-09-07
- **Deciders:** Team

## Context

mTLS proves that a client owns a private key for a certificate issued by the configured private CA, but CA trust alone does not decide whether that client may publish, consume, or modify topology. The first authorization increment must close that gap without changing the protobuf contract or coupling Core to transport security. A later increment must be able to restrict exchange and queue names.

## Decision

Operation authorization is opt-in and requires mTLS. At startup the gRPC adapter loads an immutable allowlist that maps one or more normalized certificate SHA-256 fingerprints to a stable ClientId and independent global permissions: Publish, Consume, and Admin. Multiple fingerprints support restart-based certificate rotation.

Kestrel remains responsible for certificate-chain validation during the TLS handshake. An ASP.NET Core authentication handler maps the already accepted certificate to claims without rebuilding its chain. Service-level policies protect Producer, Consumer, and Admin. An unknown fingerprint is Unauthenticated; a known client without the required permission is PermissionDenied. Configuration errors fail startup, and the default is deny when authorization is enabled.

The permission claim represents capability for an operation group. A future resource-authorization handler may add scoped grants and validate the request exchange or queue while retaining the service-level gate. Existing global permissions will keep their meaning. Ack and Nack will require a separate design for resolving a lease to its queue or owner.

## Consequences

- The protobuf wire format and .NET SDK remain source- and wire-compatible.
- Core and persistence remain independent of authentication and authorization.
- Fingerprint calculation and claim/policy checks occur per RPC, but expensive chain validation remains per TLS connection.
- Allowlist and certificate rotation require restart; a durable security audit is not implemented.
- Terminating mTLS at a proxy is outside this decision because the current identity comes from the direct Kestrel connection.
