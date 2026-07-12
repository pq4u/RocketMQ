namespace RocketMQ.Core.Models;

/// <summary>
/// A single unit of data flowing through the system, regardless of whether
/// it arrived via gRPC or the eventual Pipelines-based TCP server, and
/// regardless of whether it ends up in SQLite or the eventual custom WAL
/// manager.
///
/// Deliberately immutable (record) and deliberately free of any types from
/// Grpc.*, Microsoft.Data.Sqlite, or System.IO.Pipelines — this is a Core
/// type; adapters translate their own native structures into this type
/// and back.
///
/// Payload is ReadOnlyMemory&lt;byte&gt; (not byte[]) on purpose — the
/// eventual Pipelines adapter will operate on buffers without copying, so
/// Core should not force a copy via ToArray() up front.
/// </summary>
/// <param name="ConnectionId">
/// Connection/peer identifier assigned by the transport layer.
/// Used e.g. when sending a reply (see ITransportServer.SendAsync).
/// </param>
/// <param name="CorrelationId">Logical message identifier, independent of transport.</param>
/// <param name="Payload">Raw message data.</param>
/// <param name="ReceivedAtUtc">Timestamp when the transport accepted the message.</param>
public sealed record InboundMessage(
    Guid ConnectionId,
    Guid CorrelationId,
    ReadOnlyMemory<byte> Payload,
    DateTimeOffset ReceivedAtUtc);
