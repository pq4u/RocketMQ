using RocketMQ.Core.Abstractions;

namespace RocketMQ.Persistence.Sqlite;

/// <summary>
/// SQLite implementation of <see cref="IMessageQueueStore"/> — queue
/// semantics with visibility-timeout-based leasing, ack, and nack.
///
/// TODO (schema):
/// - Table: messages (id BLOB PK, connection_id BLOB, correlation_id BLOB,
///   payload BLOB, received_at_utc TEXT, enqueued_at_utc TEXT,
///   state TEXT DEFAULT 'available', lease_id BLOB NULL,
///   lease_expires_at_utc TEXT NULL, delivery_count INT DEFAULT 0,
///   dead_lettered_at_utc TEXT NULL, dead_letter_reason TEXT NULL)
/// - Index on (state, enqueued_at_utc) for FIFO lease queries.
/// - Open with PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;
///
/// TODO (concurrency):
/// - LeaseNextAsync: use BEGIN IMMEDIATE + UPDATE ... WHERE state='available'
///   OR (state='leased' AND lease_expires_at_utc < @now)
///   ORDER BY enqueued_at_utc LIMIT 1 RETURNING *
///   This gives pessimistic locking via SQLite's write lock (contract point 2).
/// </summary>
public sealed class SqliteMessageQueueStore : IMessageQueueStore
{
    private readonly string _connectionString;

    public SqliteMessageQueueStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public Task<Guid> EnqueueAsync(InboundMessage message, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: INSERT into messages with state='available', delivery_count=0. " +
            "Return the assigned message id. (contract point 1 — durability)");

    public Task<LeasedMessage?> LeaseNextAsync(TimeSpan visibilityTimeout, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: BEGIN IMMEDIATE; UPDATE oldest available or expired-lease row, " +
            "set state='leased', lease_id=new Guid, lease_expires_at_utc=now+timeout, " +
            "delivery_count++; COMMIT; return LeasedMessage or null. " +
            "(contract points 2, 3, 6, 8)");

    public Task AckAsync(Guid leaseId, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: DELETE FROM messages WHERE lease_id=@leaseId AND state='leased' " +
            "AND lease_expires_at_utc > @now. If rows affected = 0, throw " +
            "InvalidOperationException. (contract point 4)");

    public Task NackAsync(Guid leaseId, bool requeue, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: if requeue=true, UPDATE state='available', lease_id=NULL, " +
            "lease_expires_at_utc=NULL WHERE lease_id=@leaseId AND state='leased' " +
            "AND lease_expires_at_utc > @now. If requeue=false, UPDATE " +
            "state='dead_lettered', dead_lettered_at_utc=now. " +
            "If rows affected = 0, throw InvalidOperationException. " +
            "(contract point 5)");

    public IAsyncEnumerable<DeadLetteredMessage> BrowseDeadLettersAsync(CancellationToken ct)
        => throw new NotImplementedException(
            "TODO: SELECT * FROM messages WHERE state='dead_lettered' " +
            "ORDER BY dead_lettered_at_utc. (contract point 5 — dead-letter retrievability)");
}
