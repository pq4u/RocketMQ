namespace RocketMQ.Core.Models;

/// <summary>
/// Wraps an <see cref="InboundMessage"/> with routing metadata.
/// This is what flows through <see cref="RocketMQ.Core.Abstractions.IMessageChannel{T}"/>
/// after the transport layer constructs it from the producer's publish command.
///
/// <see cref="InboundMessage"/> stays unchanged — routing metadata lives
/// here, not on the message itself (composition over modification).
/// </summary>
/// <param name="ExchangeName">Target exchange name.</param>
/// <param name="RoutingKey">Routing key for the exchange to evaluate.</param>
/// <param name="Message">The original transport-agnostic message.</param>
public sealed record Envelope(
    string ExchangeName,
    string RoutingKey,
    InboundMessage Message);
