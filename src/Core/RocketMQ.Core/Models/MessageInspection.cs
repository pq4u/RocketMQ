namespace RocketMQ.Core.Models;

/// <summary>Metadata returned while browsing a queue without leasing a message.</summary>
public sealed record MessageSummary(
    Guid MessageId,
    Guid CorrelationId,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset EnqueuedAtUtc,
    int DeliveryCount,
    int PayloadSizeBytes,
    DateTimeOffset? LeaseExpiresAtUtc,
    DateTimeOffset? DeadLetteredAtUtc,
    string? DeadLetterReason);

/// <summary>A page of non-destructively inspected messages.</summary>
public sealed record MessagePage(
    IReadOnlyList<MessageSummary> Items,
    string? NextCursor);

/// <summary>An inspected message together with its raw payload.</summary>
public sealed record InspectedMessage(
    MessageSummary Summary,
    ReadOnlyMemory<byte> Payload);

/// <summary>Outcome of a state-sensitive administrative message mutation.</summary>
public enum MessageMutationResult
{
    Succeeded,
    NotFound,
    InvalidState
}
