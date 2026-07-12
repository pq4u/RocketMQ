namespace RocketMQ.Core.Abstractions;

/// <summary>
/// A message that was nack'd with requeue=false. Preserved for diagnostics
/// and operational visibility via
/// <see cref="IMessageQueueStore.BrowseDeadLettersAsync"/>.
///
/// This is a Core type — it must not reference any adapter-specific
/// dependencies (SQLite, Pipelines, etc.).
/// </summary>
/// <param name="MessageId">Store-assigned identifier.</param>
/// <param name="Message">The original message data.</param>
/// <param name="DeliveryCount">How many times it was leased before dead-lettering.</param>
/// <param name="DeadLetteredAtUtc">When the nack(requeue=false) occurred.</param>
/// <param name="Reason">Optional reason string (for future use, empty for now).</param>
public sealed record DeadLetteredMessage(
    Guid MessageId,
    InboundMessage Message,
    int DeliveryCount,
    DateTimeOffset DeadLetteredAtUtc,
    string Reason);
