# ADR-0006: Resource ACLs and authenticated lease ownership

- **Status:** Accepted
- **Date:** 2026-09-08
- **Deciders:** Team

## Context

ADR-0005 separates Publish, Consume and Admin, but each permission initially covered every exchange or queue. That is too broad for services which should access only their own topology. Resource checks must remain outside Core, preserve existing global grants and avoid breaking older protobuf clients. Ack and Nack require special treatment because their original request contains only a lease ID, while authorization is defined for a queue.

## Decision

The immutable startup allowlist supports global permissions and exact-name resource grants:

- `Resources:Exchanges:Publish` and `Resources:Exchanges:Admin`;
- `Resources:Queues:Consume` and `Resources:Queues:Admin`.

Names use ordinal, case-sensitive equality. Wildcards, prefixes, routing-key ACLs, tenants, runtime mutation and reload are outside this increment. A global permission retains access to every corresponding resource. Publish checks its exchange; LeaseNext checks its queue; DeclareExchange and DeclareQueue check the declared resource; Bind requires Admin for both its exchange and queue. Checks happen before invoking the underlying broker operation. Denials return PermissionDenied and are logged with identity, operation, resource and RPC, but without payload.

A lease created under authorization stores the stable `ClientId` as its owner. Ack and Nack atomically match the lease ID, active expiry, owner and, when supplied, expected queue. Foreign ownership is indistinguishable from an unknown lease and returns NotFound. Multiple certificates mapped to one ClientId therefore support rotation and cooperating processes without transferring lease ownership to a different identity.

`AckRequest.queue_name = 2` and `NackRequest.queue_name = 3` are optional additive protobuf fields. The .NET SDK supplies them as a fast path. If an older client omits the field and needs a scoped Consume check, the server resolves the active lease's queue through an indexed store query before completing it. Global and authorization-disabled calls preserve their prior behavior.

SQLite schema migration 3 adds nullable `messages.lease_owner_id`. Existing rows and active leases remain unowned, so the additive migration needs no backup or data rewrite. Unknown newer schema versions fail startup.

## Consequences

- Least-privilege access is available per exact exchange and queue without changing public SDK method signatures.
- Existing global allowlists and older wire clients remain compatible.
- Core stays independent of certificates and ACL policy; its queue-store port carries only an optional opaque owner ID and expected queue.
- Ack/Nack normally need no extra read; only older scoped clients use the lookup fallback.
- ACL and certificate changes require restart, and there is still no durable audit, wildcard policy, tenant model or runtime administration API.
