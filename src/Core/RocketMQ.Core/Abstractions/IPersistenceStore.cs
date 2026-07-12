namespace RocketMQ.Core.Abstractions;

/// <summary>
/// Port for the persistence layer. Implementations (SQLite in WAL mode to
/// start, eventually a custom low-level WAL file manager with fsync) must
/// satisfy an identical contract, verified by the shared
/// PersistenceStoreContractTests suite run against EVERY implementation.
///
/// CONTRACT:
///
/// 1. Durability: once the Task returned by AppendAsync completes, the
///    message MUST survive a process crash occurring right after that
///    moment.
///    - SQLite adapter: WAL mode + synchronous=NORMAL/FULL
///    - eventual custom adapter: explicit fsync before returning the Task
///    An implementation that buffers without fsync/commit violates the contract.
/// 2. Concurrency safety: AppendAsync must be safe to call concurrently —
///    the implementation is responsible for serializing writes (e.g. a
///    single writer connection plus a queue).
/// 3. Ordering: ReadFromAsync returns messages in write order, starting
///    strictly AFTER the given sequence number (exclusive).
/// 4. Sequence numbers are monotonically increasing and unique within a
///    single store instance — resume/replay relies on them.
/// </summary>
public interface IPersistenceStore
{
    /// <summary>
    /// Persists a message durably. Returns the sequence number assigned to
    /// it (for later resume via ReadFromAsync).
    /// </summary>
    Task<long> AppendAsync(InboundMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Returns messages written after the given sequence number, in write
    /// order. Use afterSequenceNumber = 0 to read from the beginning.
    /// </summary>
    IAsyncEnumerable<InboundMessage> ReadFromAsync(long afterSequenceNumber, CancellationToken cancellationToken);
}
