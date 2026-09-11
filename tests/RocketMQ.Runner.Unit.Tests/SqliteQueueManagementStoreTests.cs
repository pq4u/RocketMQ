using Microsoft.Data.Sqlite;
using RocketMQ.Core.Models;
using RocketMQ.Persistence.Sqlite;

namespace RocketMQ.Runner.Unit.Tests;

public sealed class SqliteQueueManagementStoreTests : IAsyncLifetime
{
    private string _databasePath = null!;
    private SqliteRoutingStore _routing = null!;
    private SqliteMessageQueueStore _queues = null!;
    private SqliteQueueManagementStore _management = null!;

    public ValueTask InitializeAsync()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"rocketmq-management-{Guid.NewGuid():N}.db");
        var database = new SqliteDatabase(
            $"Data Source={_databasePath};Mode=ReadWriteCreate;Cache=Shared;Pooling=False");
        _routing = new SqliteRoutingStore(database);
        _queues = new SqliteMessageQueueStore(database);
        _management = new SqliteQueueManagementStore(database);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        DeleteIfExists(_databasePath);
        DeleteIfExists(_databasePath + "-wal");
        DeleteIfExists(_databasePath + "-shm");
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Statistics_AndPurge_UseEffectiveQueueStates()
    {
        var ct = TestContext.Current.CancellationToken;
        await DeclareQueueAsync("orders", 10, ct);
        await _queues.EnqueueAsync("orders", NewMessage("available"), ct);
        await _queues.EnqueueAsync("orders", NewMessage("leased"), ct);
        var active = await _queues.LeaseNextAsync("orders", TimeSpan.FromMinutes(1), ct);
        await _queues.EnqueueAsync("orders", NewMessage("dead"), ct);
        var dead = await _queues.LeaseNextAsync("orders", TimeSpan.FromMinutes(1), ct);
        await _queues.NackAsync(dead!.LeaseId, requeue: false, ct);

        var before = await _management.GetStatisticsAsync("orders", ct);

        Assert.Equal(1, before.ReadyCount);
        Assert.Equal(1, before.InFlightCount);
        Assert.Equal(1, before.DeadLetterCount);
        Assert.Equal(3, before.TotalCount);
        Assert.NotNull(active);

        var deleted = await _management.PurgeReadyAsync("orders", ct);
        var after = await _management.GetStatisticsAsync("orders", ct);

        Assert.Equal(1, deleted);
        Assert.Equal(0, after.ReadyCount);
        Assert.Equal(1, after.InFlightCount);
        Assert.Equal(1, after.DeadLetterCount);
    }

    [Fact]
    public async Task BrowseReady_IsPaged_AndDoesNotLeaseMessages()
    {
        var ct = TestContext.Current.CancellationToken;
        await DeclareQueueAsync("paged", 10, ct);
        var firstId = await _queues.EnqueueAsync("paged", NewMessage("one"), ct);
        await _queues.EnqueueAsync("paged", NewMessage("two"), ct);
        await _queues.EnqueueAsync("paged", NewMessage("three"), ct);

        var first = await _management.BrowseReadyAsync("paged", null, 2, ct);
        var second = await _management.BrowseReadyAsync("paged", first.NextCursor, 2, ct);
        var detail = await _management.GetReadyAsync("paged", firstId, 64 * 1024, ct);
        var leased = await _queues.LeaseNextAsync("paged", TimeSpan.FromMinutes(1), ct);

        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        Assert.Single(second.Items);
        Assert.Null(second.NextCursor);
        Assert.NotNull(detail);
        Assert.Equal("one", System.Text.Encoding.UTF8.GetString(detail.Payload.Span));
        Assert.Equal(firstId, leased!.MessageId);
    }

    [Fact]
    public async Task RequeueDeadLetter_ResetsDeliveryCount()
    {
        var ct = TestContext.Current.CancellationToken;
        await DeclareQueueAsync("retry", 1, ct);
        var messageId = await _queues.EnqueueAsync("retry", NewMessage("retry-me"), ct);
        var leased = await _queues.LeaseNextAsync("retry", TimeSpan.FromMinutes(1), ct);
        await _queues.NackAsync(leased!.LeaseId, requeue: false, ct);

        var result = await _management.RequeueDeadLetterAsync("retry", messageId, ct);
        var retried = await _queues.LeaseNextAsync("retry", TimeSpan.FromMinutes(1), ct);

        Assert.Equal(MessageMutationResult.Succeeded, result);
        Assert.NotNull(retried);
        Assert.Equal(1, retried.DeliveryCount);
    }

    [Fact]
    public async Task Browse_RejectsInvalidCursor()
    {
        var ct = TestContext.Current.CancellationToken;
        await DeclareQueueAsync("cursor", 10, ct);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _management.BrowseReadyAsync("cursor", "not-a-cursor", 50, ct));
    }

    private Task DeclareQueueAsync(string name, int maxDeliveryCount, CancellationToken ct)
        => _routing.DeclareQueueAsync(new QueueDefinition(name, true, maxDeliveryCount), ct);

    private static InboundMessage NewMessage(string payload)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            System.Text.Encoding.UTF8.GetBytes(payload),
            DateTimeOffset.UtcNow);

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
