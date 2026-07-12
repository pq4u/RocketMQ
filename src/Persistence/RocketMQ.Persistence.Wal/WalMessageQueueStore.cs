using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;

namespace RocketMQ.Persistence.Wal;

/// <summary>
/// Target, low-level implementation of <see cref="IMessageQueueStore"/>:
/// a purpose-built WAL-backed queue manager using direct file writes and
/// explicit fsync, meant to replace
/// <see cref="SqliteMessageQueueStore"/> once SQLite's throughput becomes
/// the bottleneck.
///
/// Swapping this in is only a DI registration change — Core must not need
/// to change at all.
///
/// TODO (design — this is significantly more complex than the log variant):
/// - Need an on-disk structure that supports state transitions (available →
///   leased → acked/dead-lettered) without the luxury of SQL UPDATE.
///   Consider: append-only state log + in-memory index rebuilt on startup,
///   or a B-tree/LSM-based approach.
/// - Visibility timeout expiry: either scan-on-lease or background compactor.
/// - Must satisfy the same 8-point contract as SqliteMessageQueueStore.
/// </summary>
public sealed class WalMessageQueueStore : IMessageQueueStore
{
    private readonly string _filePath;

    public WalMessageQueueStore(string filePath)
    {
        _filePath = filePath;
    }

    public Task<Guid> EnqueueAsync(InboundMessage message, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: append enqueue record to the log file, fsync, return message id. " +
            "(contract point 1 — durability)");

    public Task<LeasedMessage?> LeaseNextAsync(TimeSpan visibilityTimeout, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: find oldest available message in the in-memory index, append " +
            "lease record to log, fsync, update index, return LeasedMessage or null. " +
            "(contract points 2, 3, 6, 8)");

    public Task AckAsync(Guid leaseId, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: validate lease is active in index, append ack record to log, " +
            "fsync, remove from index. Throw InvalidOperationException if lease " +
            "is not active. (contract point 4)");

    public Task NackAsync(Guid leaseId, bool requeue, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: validate lease is active in index. If requeue=true, append " +
            "nack-requeue record, update index to available. If requeue=false, " +
            "append dead-letter record, update index. Throw " +
            "InvalidOperationException if lease is not active. (contract point 5)");

    public IAsyncEnumerable<DeadLetteredMessage> BrowseDeadLettersAsync(CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: scan dead-letter entries from the in-memory index. " +
            "(contract point 5 — dead-letter retrievability)");
}
