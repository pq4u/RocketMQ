using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;

namespace RocketMQ.Persistence.Wal;

/// <summary>
/// Target, low-level implementation of <see cref="IPersistenceStore"/>:
/// a purpose-built WAL file manager using direct file writes and explicit
/// fsync, meant to replace
/// <c>MyProject.Adapters.Persistence.Sqlite</c> once the SQLite adapter's
/// throughput becomes the bottleneck. Swapping this in is only a DI
/// registration change in Host — Core must not need to change at all.
///
/// TODO (implementation pass — this is the hard one, take your time):
/// - Append-only log file: [seq][length][payload][checksum] per record.
/// - AppendAsync must call fsync (FileStream.FlushAsync(flushToDisk: true)
///   on .NET, or explicit fsync via a SafeFileHandle write path) BEFORE
///   the returned Task completes — this is what makes contract point 1
///   (durability) hold without SQLite's WAL machinery underneath you.
/// - Group commits (batch fsync across concurrently pending AppendAsync
///   calls) are the standard technique to keep throughput acceptable once
///   every write forces an fsync — worth benchmarking against a naive
///   per-call fsync before committing to one approach.
/// - Crash recovery on startup: scan the log for the last valid record
///   (checksum mismatch or truncated write = stop there, ignore the rest).
/// - ReadFromAsync: maintain an in-memory or on-disk index from seq to
///   file offset so this isn't a linear scan for every resume.
/// </summary>
public sealed class CustomWalPersistenceStore : IPersistenceStore
{
    private readonly string _filePath;

    public CustomWalPersistenceStore(string filePath)
    {
        _filePath = filePath;
    }

    public Task<long> AppendAsync(InboundMessage message, CancellationToken cancellationToken)
        => throw new NotImplementedException(
            "TODO: append record to the log file, fsync, return seq only after fsync completes (contract point 1)");

    public IAsyncEnumerable<InboundMessage> ReadFromAsync(long afterSequenceNumber, CancellationToken cancellationToken)
        => throw new NotImplementedException(
            "TODO: use the seq->offset index to seek and stream records after afterSequenceNumber (contract point 3)");
}
