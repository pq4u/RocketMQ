using Microsoft.Extensions.Configuration;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Management.Api;
using RocketMQ.Management.Contracts;
using RocketMQ.Runner;

namespace RocketMQ.Runner.Unit.Tests;

public sealed class ManagementApplicationServiceTests
{
    [Fact]
    public async Task Publish_maps_request_and_preserves_client_identifiers()
    {
        var publisher = new RecordingPublisher();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(publisher, clock: clock);
        var publishId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        var result = await service.PublishAsync(
            new PublishMessageRequest(
                "orders",
                "created.eu",
                "utf8",
                "zażółć",
                CorrelationId: correlationId,
                PublishId: publishId),
            CancellationToken.None);

        Assert.Equal(publishId, result.PublishId);
        Assert.Equal(PublishStatus.Accepted.ToString(), result.Status);
        Assert.Equal(["queue-a"], result.DestinationQueues);
        Assert.Equal(publishId, publisher.PublishId);
        Assert.NotNull(publisher.Envelope);
        Assert.Equal("orders", publisher.Envelope.ExchangeName);
        Assert.Equal("created.eu", publisher.Envelope.RoutingKey);
        Assert.Equal(correlationId, publisher.Envelope.Message.CorrelationId);
        Assert.Equal("zażółć", System.Text.Encoding.UTF8.GetString(publisher.Envelope.Message.Payload.Span));
        Assert.Equal(clock.GetUtcNow(), publisher.Envelope.Message.ReceivedAtUtc);
    }

    [Fact]
    public async Task Publish_rejects_invalid_base64_before_calling_publisher()
    {
        var publisher = new RecordingPublisher();
        var service = CreateService(publisher);

        await Assert.ThrowsAsync<ManagementValidationException>(() => service.PublishAsync(
            new PublishMessageRequest("orders", "", "base64", "%%%", null, null),
            CancellationToken.None));

        Assert.Null(publisher.Envelope);
    }

    [Fact]
    public async Task Publish_rejects_payload_larger_than_one_mebibyte()
    {
        var publisher = new RecordingPublisher();
        var service = CreateService(publisher);
        var payload = new string('x', ManagementApplicationService.MaximumPublishPayloadBytes + 1);

        await Assert.ThrowsAsync<ManagementPayloadTooLargeException>(() => service.PublishAsync(
            new PublishMessageRequest("orders", "", "utf8", payload, null, null),
            CancellationToken.None));

        Assert.Null(publisher.Envelope);
    }

    [Fact]
    public void Metrics_returns_zero_filled_minute_buckets_and_recorded_counters()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 11, 10, 12, 34, TimeSpan.Zero));
        var metrics = new BrokerMetrics(clock);
        metrics.RecordPublishAttempt();
        metrics.RecordPublishResult(new PublishResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PublishStatus.Accepted,
            ["queue-a", "queue-b"]));
        metrics.RecordDelivery();
        metrics.RecordAck();
        metrics.RecordNack(requeue: true);
        metrics.RecordNack(requeue: false);

        var buckets = metrics.Snapshot(TimeSpan.FromMinutes(15));

        Assert.Equal(15, buckets.Count);
        Assert.All(buckets.Take(14), bucket =>
        {
            Assert.Equal(0, bucket.PublishAttempts);
            Assert.Equal(0, bucket.Deliveries);
        });
        var current = buckets[^1];
        Assert.Equal(new DateTimeOffset(2026, 9, 11, 10, 12, 0, TimeSpan.Zero), current.StartedAtUtc);
        Assert.Equal(1, current.PublishAttempts);
        Assert.Equal(1, current.AcceptedPublishes);
        Assert.Equal(2, current.RoutedCopies);
        Assert.Equal(1, current.Deliveries);
        Assert.Equal(1, current.Acks);
        Assert.Equal(1, current.RequeuedNacks);
        Assert.Equal(1, current.DeadLetterNacks);
    }

    [Fact]
    public void Authorization_snapshot_masks_certificate_fingerprints()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RocketMQ:Security:Authorization:Enabled"] = "true",
                ["RocketMQ:Security:Authorization:Clients:worker:Permissions:0"] = "consume",
                ["RocketMQ:Security:Authorization:Clients:worker:Resources:Queues:Consume:0"] = "orders.*",
                ["RocketMQ:Security:Authorization:Clients:worker:CertificateSha256Fingerprints:0"] =
                    "AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77"
            })
            .Build();
        var service = CreateService(new RecordingPublisher(), configuration: configuration);

        var snapshot = service.GetAuthorizationSnapshot();

        Assert.True(snapshot.Enabled);
        var client = Assert.Single(snapshot.Clients);
        Assert.Equal("worker", client.ClientId);
        Assert.Equal(["consume"], client.GlobalPermissions);
        Assert.Equal(["orders.*"], client.ConsumeQueues);
        Assert.Equal(["…44556677"], client.MaskedFingerprints);
    }

    [Fact]
    public async Task Browse_rejects_page_size_outside_public_contract()
    {
        var service = CreateService(new RecordingPublisher());

        await Assert.ThrowsAsync<ManagementValidationException>(() => service.BrowseReadyAsync(
            "orders",
            cursor: null,
            limit: ManagementApplicationService.MaximumPageSize + 1,
            CancellationToken.None));
    }

    private static ManagementApplicationService CreateService(
        IMessagePublisher publisher,
        IConfiguration? configuration = null,
        ManualTimeProvider? clock = null)
    {
        var timeProvider = clock ?? new ManualTimeProvider(DateTimeOffset.UtcNow);
        return new ManagementApplicationService(
            new InMemoryRoutingStore(),
            new NoopQueueManagementStore(),
            publisher,
            new BrokerMetrics(timeProvider),
            configuration ?? new ConfigurationBuilder().Build(),
            timeProvider);
    }

    private sealed class RecordingPublisher : IMessagePublisher
    {
        public Guid? PublishId { get; private set; }
        public Envelope? Envelope { get; private set; }

        public Task<PublishResult> PublishAsync(Guid publishId, Envelope envelope, CancellationToken ct)
        {
            PublishId = publishId;
            Envelope = envelope;
            return Task.FromResult(new PublishResult(
                publishId,
                Guid.NewGuid(),
                PublishStatus.Accepted,
                ["queue-a"]));
        }
    }

    private sealed class NoopQueueManagementStore : IQueueManagementStore
    {
        public Task<QueueStatistics> GetStatisticsAsync(string queueName, CancellationToken ct)
            => Task.FromResult(new QueueStatistics(queueName, 0, 0, 0, null));

        public Task<MessagePage> BrowseReadyAsync(
            string queueName,
            string? cursor,
            int limit,
            CancellationToken ct)
            => Task.FromResult(new MessagePage([], null));

        public Task<InspectedMessage?> GetReadyAsync(
            string queueName,
            Guid messageId,
            int maxPayloadBytes,
            CancellationToken ct)
            => Task.FromResult<InspectedMessage?>(null);

        public Task<MessagePage> BrowseDeadLettersAsync(
            string queueName,
            string? cursor,
            int limit,
            CancellationToken ct)
            => Task.FromResult(new MessagePage([], null));

        public Task<InspectedMessage?> GetDeadLetterAsync(
            string queueName,
            Guid messageId,
            int maxPayloadBytes,
            CancellationToken ct)
            => Task.FromResult<InspectedMessage?>(null);

        public Task<int> PurgeReadyAsync(string queueName, CancellationToken ct)
            => Task.FromResult(0);

        public Task<MessageMutationResult> RequeueDeadLetterAsync(
            string queueName,
            Guid messageId,
            CancellationToken ct)
            => Task.FromResult(MessageMutationResult.NotFound);

        public Task<MessageMutationResult> DeleteDeadLetterAsync(
            string queueName,
            Guid messageId,
            CancellationToken ct)
            => Task.FromResult(MessageMutationResult.NotFound);

        public Task<int> ClearDeadLettersAsync(string queueName, CancellationToken ct)
            => Task.FromResult(0);

        public Task<bool> CheckHealthAsync(CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
