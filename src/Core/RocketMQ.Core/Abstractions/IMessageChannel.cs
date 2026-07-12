namespace RocketMQ.Core.Abstractions;

/// <summary>
/// Port for the backpressure boundary between transport and persistence.
/// The implementation wraps System.Threading.Channels with a bounded
/// capacity and BoundedChannelFullMode.Wait — this interface exists so
/// that Core depends on the BEHAVIOR (bounded capacity, waiting instead of
/// dropping/overwriting), not directly on System.Threading.Channels.
///
/// CONTRACT — verified by MessageChannelContractTests:
///
/// 1. WriteAsync completes only once capacity is available. Messages must
///    NEVER be silently dropped (this rules out
///    BoundedChannelFullMode.DropWrite/DropOldest in the implementation).
/// 2. ReadAllAsync completes iteration only after Complete() has been
///    called and the buffer has been drained — the standard "producer
///    signals completion" pattern.
/// 3. The implementation must be safe for multiple concurrent producers
///    and a single consumer (single reader), unless explicitly documented
///    otherwise in a specific implementation.
///
/// System.Threading.Channels adapter note: capacity and FullMode must
/// ALWAYS be explicit in the constructor/DI — never
/// Channel.CreateUnbounded on the network→disk production path (see
/// CLAUDE.md, Channels section).
/// </summary>
/// <typeparam name="T">Element type flowing through the channel (e.g. InboundMessage).</typeparam>
public interface IMessageChannel<T>
{
    /// <summary>Writes an item, waiting for available capacity (backpressure).</summary>
    ValueTask WriteAsync(T item, CancellationToken cancellationToken);

    /// <summary>Reads all items until Complete() is called and the buffer is drained.</summary>
    IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken);

    /// <summary>Signals to the consumer that no more writes will occur.</summary>
    void Complete();
}
