namespace RocketMQ.Core.Models;

/// <summary>
/// Metadata for a named queue. Does not hold messages itself —
/// messages are stored via <see cref="RocketMQ.Core.Abstractions.IMessageQueueStore"/>.
/// </summary>
/// <param name="Name">Unique queue name.</param>
/// <param name="Durable">If true, survives broker restart.</param>
/// <param name="MaxDeliveryCount">After N leases → auto dead-letter. 0 = unlimited.</param>
public sealed record QueueDefinition(
    string Name,
    bool Durable,
    int MaxDeliveryCount);
