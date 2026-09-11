namespace RocketMQ.Management.Contracts;

public sealed record ExchangeDto(string Name, string Type, bool Durable);

public sealed record DeclareExchangeRequest(string Type);

public sealed record QueueDto(string Name, bool Durable, int MaxDeliveryCount);

public sealed record DeclareQueueRequest(int MaxDeliveryCount = 10);

public sealed record BindingDto(string ExchangeName, string QueueName, string RoutingKey);

public sealed record CreateBindingRequest(
    string ExchangeName,
    string QueueName,
    string RoutingKey);

public sealed record QueueStatisticsDto(
    string QueueName,
    long ReadyCount,
    long InFlightCount,
    long DeadLetterCount,
    long TotalCount,
    DateTimeOffset? OldestReadyAtUtc);

public sealed record MessageSummaryDto(
    Guid MessageId,
    Guid CorrelationId,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset EnqueuedAtUtc,
    int DeliveryCount,
    int PayloadSizeBytes,
    DateTimeOffset? LeaseExpiresAtUtc,
    DateTimeOffset? DeadLetteredAtUtc,
    string? DeadLetterReason);

public sealed record PayloadPreviewDto(
    int ByteLength,
    string PreviewBase64,
    string? PreviewText,
    bool Truncated);

public sealed record MessageDetailDto(
    MessageSummaryDto Message,
    PayloadPreviewDto Payload);

public sealed record PageDto<T>(
    IReadOnlyList<T> Items,
    string? NextCursor);

public sealed record CountResultDto(int AffectedCount);

public sealed record PublishMessageRequest(
    string ExchangeName,
    string RoutingKey,
    string PayloadEncoding,
    string Payload,
    Guid? CorrelationId = null,
    Guid? PublishId = null);

public sealed record PublishMessageResultDto(
    Guid PublishId,
    Guid MessageId,
    string Status,
    IReadOnlyList<string> DestinationQueues);

public sealed record BrokerOverviewDto(
    string Version,
    DateTimeOffset StartedAtUtc,
    long UptimeSeconds,
    int ExchangeCount,
    int QueueCount,
    int BindingCount,
    long ReadyCount,
    long InFlightCount,
    long DeadLetterCount);

public sealed record MetricBucketDto(
    DateTimeOffset StartedAtUtc,
    long PublishAttempts,
    long AcceptedPublishes,
    long UnroutablePublishes,
    long RoutedCopies,
    long Deliveries,
    long Acks,
    long RequeuedNacks,
    long DeadLetterNacks);

public sealed record MetricsDto(
    string Range,
    IReadOnlyList<MetricBucketDto> Buckets);

public sealed record ManagementConfigurationDto(
    string ManagementUrl,
    string GrpcUrl,
    string DatabasePath,
    int PublishBatchSize,
    string PublishBatchDelay,
    bool MutualTlsEnabled,
    bool AuthorizationEnabled,
    int DeadLetterRetentionDays,
    int PublishIdRetentionHours);

public sealed record AuthorizedClientDto(
    string ClientId,
    IReadOnlyList<string> GlobalPermissions,
    IReadOnlyList<string> PublishExchanges,
    IReadOnlyList<string> AdminExchanges,
    IReadOnlyList<string> ConsumeQueues,
    IReadOnlyList<string> AdminQueues,
    IReadOnlyList<string> MaskedFingerprints);

public sealed record AuthorizationSnapshotDto(
    bool Enabled,
    IReadOnlyList<AuthorizedClientDto> Clients);
