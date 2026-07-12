using RocketMQ.Core.Models;

namespace RocketMQ.Core.Abstractions;

/// <summary>
/// Port for the message queue persistence layer. Implementations (SQLite
/// to start, eventually other backends) must satisfy an identical contract,
/// verified by the shared MessageQueueStoreContractTests suite run against
/// EVERY implementation.
///
/// This port models a competing-consumers queue with visibility-timeout-based
/// lease semantics (RabbitMQ style), NOT an append-only log (Kafka style).
///
/// CONTRACT:
///
/// 1. Durability: once the Task returned by EnqueueAsync completes, the
///    message MUST survive a process crash occurring right after that moment.
///    Same durability guarantees as IPersistenceStore contract point 1.
///
/// 2. Atomicity of lease: LeaseNextAsync must atomically find the
///    oldest available (non-leased, non-acked) message and mark it as leased
///    with the given visibilityTimeout. If no message is available, it
///    returns null. Two concurrent LeaseNextAsync calls must NEVER return
///    the same message — the implementation must use pessimistic locking,
///    CAS, or equivalent to guarantee this.
///
/// 3. Visibility timeout: a leased message becomes available for
///    re-delivery automatically once visibilityTimeout has elapsed WITHOUT
///    a prior AckAsync or NackAsync call for that lease. The implementation
///    is NOT required to use a background timer — it is sufficient that
///    LeaseNextAsync considers expired leases as available. The
///    DeliveryCount of the message MUST be incremented on each re-delivery.
///
/// 4. Ack semantics: AckAsync permanently removes the message from the
///    queue. After a successful AckAsync, the message MUST NOT be returned
///    by any future LeaseNextAsync call. AckAsync MUST throw
///    InvalidOperationException if the leaseId does not correspond to
///    a currently active (non-expired) lease.
///
/// 5. Nack semantics:
///    - requeue=true: the message is immediately returned to the pool of
///      available messages (lease is released). DeliveryCount is preserved
///      (it was already incremented at lease time). The message becomes
///      immediately eligible for LeaseNextAsync.
///    - requeue=false: the message is moved to the dead-letter state.
///      It MUST NOT be returned by future LeaseNextAsync calls. It CAN
///      be retrieved via BrowseDeadLettersAsync for diagnostics.
///    NackAsync MUST throw InvalidOperationException if the leaseId does
///    not correspond to a currently active (non-expired) lease.
///
/// 6. Ordering: LeaseNextAsync returns the oldest available message
///    (FIFO within the set of available messages). "Oldest" is defined
///    by enqueue order.
///
/// 7. Concurrency safety: all methods must be safe to call concurrently.
///    The implementation is responsible for serialization where needed.
///
/// 8. DeliveryCount tracking: each message tracks how many times it has
///    been leased (initial lease = 1, first re-delivery = 2, etc.).
///    This counter is available on the returned LeasedMessage and persists
///    across nack-requeue and visibility-timeout re-deliveries.
/// </summary>
public interface IMessageQueueStore
{
    /// <summary>
    /// Persists a message durably into the queue. Returns a store-assigned
    /// unique message identifier.
    /// Contract points: 1 (durability), 7 (concurrency).
    /// </summary>
    Task<Guid> EnqueueAsync(InboundMessage message, CancellationToken ct);

    /// <summary>
    /// Atomically leases the next available message, making it invisible
    /// to other consumers for the duration of visibilityTimeout. Returns
    /// null if the queue has no available messages.
    /// Contract points: 2 (atomicity), 3 (visibility timeout), 6 (FIFO),
    ///                   8 (DeliveryCount).
    /// </summary>
    Task<LeasedMessage?> LeaseNextAsync(TimeSpan visibilityTimeout, CancellationToken ct);

    /// <summary>
    /// Permanently removes the message from the queue. The leaseId must
    /// correspond to a currently active (non-expired) lease.
    /// Contract points: 4 (ack semantics).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when leaseId does not correspond to an active lease.
    /// </exception>
    Task AckAsync(Guid leaseId, CancellationToken ct);

    /// <summary>
    /// Releases or dead-letters the message.
    /// - requeue=true  → message returns to available pool immediately.
    /// - requeue=false → message moves to dead-letter state.
    /// The leaseId must correspond to a currently active (non-expired) lease.
    /// Contract points: 5 (nack semantics), 8 (DeliveryCount preserved).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when leaseId does not correspond to an active lease.
    /// </exception>
    Task NackAsync(Guid leaseId, bool requeue, CancellationToken ct);

    /// <summary>
    /// Returns dead-lettered messages for diagnostic/operational purposes.
    /// This is a read-only browse — messages are not removed.
    /// Contract point: 5 (dead-letter retrievability).
    /// </summary>
    IAsyncEnumerable<DeadLetteredMessage> BrowseDeadLettersAsync(CancellationToken ct);
}
