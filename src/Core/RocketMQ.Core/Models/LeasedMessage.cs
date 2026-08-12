using RocketMQ.Core.Abstractions;

namespace RocketMQ.Core.Models;

/// <summary>
/// Represents a message leased from a queue. MessageId is stable across
/// redeliveries; LeaseId identifies only this delivery attempt.
/// </summary>
public sealed record LeasedMessage(
    Guid MessageId,
    Guid LeaseId,
    InboundMessage Message,
    int DeliveryCount,
    DateTimeOffset LeaseExpiresAtUtc);
