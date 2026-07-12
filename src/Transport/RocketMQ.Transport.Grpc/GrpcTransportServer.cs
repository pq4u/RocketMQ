using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;

namespace RocketMQ.Transport.Grpc;

/// <summary>
/// gRPC implementation of <see cref="ITransportServer"/> — the starting
/// transport used to get a working prototype fast. Will eventually be
/// replaced by <c>MyProject.Adapters.Transport.Pipelines</c> without any
/// changes to Core, as long as this class keeps honoring the contract
/// documented on ITransportServer.
///
/// TODO (first implementation pass):
/// - Host a Grpc.AspNetCore service that receives frames and, for each
///   one, builds an <see cref="Envelope"/> and writes it to the
///   injected <see cref="IMessageChannel{T}"/> (contract point 2 —
///   never a "Receive" method on this class itself).
/// - Track connectionId -> gRPC stream/call mapping so SendAsync can
///   route a reply to the right peer.
/// - StopAsync must let in-flight SendAsync calls finish (contract point 3).
/// </summary>
public sealed class GrpcTransportServer : ITransportServer
{
    private readonly IMessageChannel<Envelope> _inboundChannel;

    public GrpcTransportServer(IMessageChannel<Envelope> inboundChannel)
    {
        _inboundChannel = inboundChannel;
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException(
            "TODO: start Grpc.AspNetCore host, wrap incoming frames into Envelope and write via _inboundChannel.WriteAsync");

    public Task StopAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException(
            "TODO: stop host gracefully, await in-flight SendAsync calls before returning");

    public Task SendAsync(Guid connectionId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        => throw new NotImplementedException(
            "TODO: look up the gRPC stream for connectionId and write payload to it; " +
            "throw InvalidOperationException if connectionId is unknown/closed (contract point 4)");
}
