namespace RocketMQ.Core.Models;

/// <summary>Operational message counts for a named queue at a point in time.</summary>
public sealed record QueueStatistics(
    string QueueName,
    long ReadyCount,
    long InFlightCount,
    long DeadLetterCount,
    DateTimeOffset? OldestReadyAtUtc)
{
    public long TotalCount => ReadyCount + InFlightCount + DeadLetterCount;
}
