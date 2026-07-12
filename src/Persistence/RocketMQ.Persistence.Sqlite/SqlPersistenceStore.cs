using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;

namespace RocketMQ.Persistence.Sqlite;

/// <summary>
/// SQLite implementation of <see cref="IPersistenceStore"/> — the starting
/// persistence layer. Will eventually be replaced by
/// <c>MyProject.Adapters.Persistence.CustomWal</c> without any changes to
/// Core, as long as this class keeps honoring the contract documented on
/// IPersistenceStore.
///
/// TODO (first implementation pass):
/// - Open the database with "Mode=ReadWriteCreate" and immediately run
///   PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;
///   (synchronous=FULL if you need the stronger durability guarantee —
///   this is the "durability" half of contract point 1).
/// - Use a single dedicated writer connection (or a serialized queue) for
///   AppendAsync — SQLite allows one writer at a time even in WAL mode;
///   this satisfies contract point 2 (concurrency safety) without
///   throwing SQLITE_BUSY under load. Set a busy_timeout as a backstop.
/// - The Task from AppendAsync must only complete after the write is
///   actually committed — do not return early from a buffered/batched
///   write that hasn't hit the WAL file yet.
/// - ReadFromAsync: "SELECT ... WHERE seq > @after ORDER BY seq" — do not
///   forget the index on the sequence column or this degrades badly.
/// </summary>
public sealed class SqlitePersistenceStore : IPersistenceStore
{
    private readonly string _connectionString;

    public SqlitePersistenceStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public Task<long> AppendAsync(InboundMessage message, CancellationToken cancellationToken)
        => throw new NotImplementedException(
            "TODO: insert on the dedicated writer connection, return the assigned seq number " +
            "only after the write is committed (contract point 1)");

    public IAsyncEnumerable<InboundMessage> ReadFromAsync(long afterSequenceNumber, CancellationToken cancellationToken)
        => throw new NotImplementedException(
            "TODO: SELECT ... WHERE seq > @afterSequenceNumber ORDER BY seq, stream results (contract point 3)");
}
