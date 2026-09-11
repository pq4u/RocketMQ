using RocketMQ.Core.Models;

namespace RocketMQ.Core.Abstractions;

/// <summary>
/// Administrative queue inspection and mutation operations. These operations are
/// deliberately separate from <see cref="IMessageQueueStore"/> so consumer
/// delivery semantics remain independent from management capabilities.
/// </summary>
public interface IQueueManagementStore
{
    Task<QueueStatistics> GetStatisticsAsync(string queueName, CancellationToken ct);

    Task<MessagePage> BrowseReadyAsync(
        string queueName,
        string? cursor,
        int limit,
        CancellationToken ct);

    Task<InspectedMessage?> GetReadyAsync(
        string queueName,
        Guid messageId,
        int maxPayloadBytes,
        CancellationToken ct);

    Task<MessagePage> BrowseDeadLettersAsync(
        string queueName,
        string? cursor,
        int limit,
        CancellationToken ct);

    Task<InspectedMessage?> GetDeadLetterAsync(
        string queueName,
        Guid messageId,
        int maxPayloadBytes,
        CancellationToken ct);

    Task<int> PurgeReadyAsync(string queueName, CancellationToken ct);

    Task<MessageMutationResult> RequeueDeadLetterAsync(
        string queueName,
        Guid messageId,
        CancellationToken ct);

    Task<MessageMutationResult> DeleteDeadLetterAsync(
        string queueName,
        Guid messageId,
        CancellationToken ct);

    Task<int> ClearDeadLettersAsync(string queueName, CancellationToken ct);

    Task<bool> CheckHealthAsync(CancellationToken ct);
}
