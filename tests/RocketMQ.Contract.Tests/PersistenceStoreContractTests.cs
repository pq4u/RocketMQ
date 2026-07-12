using RocketMQ.Core.Abstractions;
using Xunit;

namespace RocketMQ.Contract.Tests;

/// <summary>
/// The single source of truth for "is this an IPersistenceStore
/// implementation." Every adapter — SQLite today, the custom WAL manager
/// tomorrow — inherits this class and only has to implement
/// <see cref="CreateStoreAsync"/>. If both subclasses pass, the two
/// implementations are behaviorally interchangeable, which is the entire
/// point of the port/adapter split.
///
/// Do not weaken these tests to make an adapter pass. If an adapter can't
/// satisfy one of them, that's the adapter's bug, not the test's.
/// </summary>
public abstract class PersistenceStoreContractTests : IAsyncLifetime
{
    private IPersistenceStore _store = null!;

    /// <summary>Creates a fresh, empty store instance for one test.</summary>
    protected abstract Task<IPersistenceStore> CreateStoreAsync();

    /// <summary>Override to clean up whatever CreateStoreAsync allocated (temp files, connections, ...).</summary>
    protected virtual Task DisposeStoreAsync() => Task.CompletedTask;

    public async ValueTask InitializeAsync() => _store = await CreateStoreAsync();

    public async ValueTask DisposeAsync() => await DisposeStoreAsync();

    private static InboundMessage NewMessage() => new(
        ConnectionId: Guid.NewGuid(),
        CorrelationId: Guid.NewGuid(),
        Payload: new byte[] { 1, 2, 3 },
        ReceivedAtUtc: DateTimeOffset.UtcNow);

    [Fact]
    public async Task AppendAsync_Then_ReadFromZero_Returns_The_Message()
    {
        var message = NewMessage();
        await _store.AppendAsync(message, CancellationToken.None);

        var results = await CollectAsync(_store.ReadFromAsync(0, CancellationToken.None));

        Assert.Contains(results, m => m.CorrelationId == message.CorrelationId);
    }

    [Fact]
    public async Task AppendAsync_Returns_Monotonically_Increasing_Sequence_Numbers()
    {
        var seq1 = await _store.AppendAsync(NewMessage(), CancellationToken.None);
        var seq2 = await _store.AppendAsync(NewMessage(), CancellationToken.None);

        Assert.True(
            seq2 > seq1,
            $"Expected seq2 ({seq2}) > seq1 ({seq1}) — sequence numbers must be monotonically increasing (contract point 4).");
    }

    [Fact]
    public async Task ReadFromAsync_Excludes_The_Given_Sequence_Number()
    {
        var first = NewMessage();
        var seq1 = await _store.AppendAsync(first, CancellationToken.None);
        var second = NewMessage();
        await _store.AppendAsync(second, CancellationToken.None);

        var results = await CollectAsync(_store.ReadFromAsync(seq1, CancellationToken.None));

        Assert.DoesNotContain(results, m => m.CorrelationId == first.CorrelationId);
        Assert.Contains(results, m => m.CorrelationId == second.CorrelationId);
    }

    [Fact]
    public async Task ReadFromAsync_Returns_Messages_In_Write_Order()
    {
        var written = new List<InboundMessage>();
        for (var i = 0; i < 5; i++)
        {
            var message = NewMessage();
            written.Add(message);
            await _store.AppendAsync(message, CancellationToken.None);
        }

        var results = await CollectAsync(_store.ReadFromAsync(0, CancellationToken.None));
        var writtenIds = written.Select(m => m.CorrelationId).ToList();
        var readIdsInWrittenSet = results
            .Select(m => m.CorrelationId)
            .Where(id => writtenIds.Contains(id))
            .ToList();

        Assert.Equal(writtenIds, readIdsInWrittenSet);
    }

    [Fact]
    public async Task Concurrent_Appends_Do_Not_Lose_Messages()
    {
        const int concurrency = 20;
        var messages = Enumerable.Range(0, concurrency).Select(_ => NewMessage()).ToList();

        await Task.WhenAll(messages.Select(m => _store.AppendAsync(m, CancellationToken.None)));

        var results = await CollectAsync(_store.ReadFromAsync(0, CancellationToken.None));
        var resultIds = results.Select(m => m.CorrelationId).ToHashSet();

        foreach (var message in messages)
        {
            Assert.Contains(message.CorrelationId, resultIds);
        }
    }

    [Fact]
    public async Task AppendAsync_Result_Is_Immediately_Visible_To_A_Fresh_Read()
    {
        // This documents the observable half of the durability contract
        // (point 1 on IPersistenceStore). It does NOT prove durability
        // across an actual process crash — that needs an adapter-specific
        // fault-injection test (e.g. kill -9 right after fsync for
        // CustomWalPersistenceStore). What it does prove: AppendAsync
        // must not return before the write is committed/queryable.
        var message = NewMessage();
        var seq = await _store.AppendAsync(message, CancellationToken.None);

        var results = await CollectAsync(_store.ReadFromAsync(seq - 1, CancellationToken.None));

        Assert.Contains(results, m => m.CorrelationId == message.CorrelationId);
    }

    private static async Task<List<InboundMessage>> CollectAsync(IAsyncEnumerable<InboundMessage> source)
    {
        var list = new List<InboundMessage>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }
}

