namespace RocketMQ.Core.Models;

/// <summary>Durable outcome of a producer publish request.</summary>
public sealed record PublishResult(
    Guid PublishId,
    Guid MessageId,
    PublishStatus Status,
    IReadOnlyList<string> DestinationQueues);
