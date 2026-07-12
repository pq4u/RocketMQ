namespace RocketMQ.Core.Abstractions;

/// <summary>
/// Represents a message that has been leased from the queue. Contains
/// the original message data plus lease metadata needed for ack/nack.
///
/// This is a Core type — it must not reference any adapter-specific
/// dependencies (SQLite, Pipelines, etc.).
/// </summary>
/// <param name="LeaseId">Unique identifier for this specific lease.
///   Used in <see cref="IMessageQueueStore.AckAsync"/> and
///   <see cref="IMessageQueueStore.NackAsync"/>.</param>
/// <param name="Message">The original enqueued message.</param>
/// <param name="DeliveryCount">How many times this message has been leased
///   (1 = first delivery, 2 = first re-delivery, etc.). See
///   IMessageQueueStore contract point 8.</param>
/// <param name="LeaseExpiresAtUtc">When the visibility timeout expires
///   and the message becomes available for re-delivery.</param>
public sealed record LeasedMessage(
    Guid LeaseId,
    InboundMessage Message,
    int DeliveryCount,
    DateTimeOffset LeaseExpiresAtUtc);
