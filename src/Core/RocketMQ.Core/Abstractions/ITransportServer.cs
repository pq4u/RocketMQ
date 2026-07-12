namespace RocketMQ.Core.Abstractions;

/// <summary>
/// Port for the network transport layer. Implementations (gRPC to start,
/// eventually a custom TCP server on System.IO.Pipelines) must be fully
/// interchangeable — Core must never depend on a specific transport package
/// (Grpc.*, System.IO.Pipelines).
///
/// CONTRACT — must be satisfied identically by every implementation and
/// verified by TransportContractTests:
///
/// 1. StartAsync does not block indefinitely — it returns once the server
///    is ready to accept connections (listening started).
/// 2. Every received message must be pushed into the
///    <see cref="IMessageChannel{T}"/> injected into the implementation via
///    DI. ITransportServer deliberately has no "Receive" method — data
///    flows one way, into the channel, and it's the channel's consumer
///    (Host) that controls the rate of consumption.
/// 3. StopAsync completes only once all in-flight sends have finished, or
///    once the supplied CancellationToken fires — connections must not be
///    "cut" mid-send.
/// 4. SendAsync must throw if connectionId doesn't exist or has been
///    closed — data must never be silently dropped.
///
/// Pipelines adapter note: the incoming payload arrives as a
/// ReadOnlySequence&lt;byte&gt; that is short-lived (the buffer gets
/// reused) — when building an InboundMessage, copy the data into your own
/// memory BEFORE calling writer.AdvanceTo; never hold a reference to the
/// raw buffer.
/// </summary>
public interface ITransportServer
{
    /// <summary>Starts listening. Does not block — returns once the server has started.</summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Stops the server, letting in-flight sends finish first (see contract point 3).</summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sends data to a specific, previously connected peer.
    /// The implementation translates the payload into its own wire format
    /// (protobuf for gRPC, a length-prefixed frame for Pipelines).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when connectionId does not correspond to any active connection.
    /// </exception>
    Task SendAsync(Guid connectionId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}
