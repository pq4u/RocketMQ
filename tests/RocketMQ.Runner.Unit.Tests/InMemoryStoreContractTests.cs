using RocketMQ.Contract.Tests;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Persistence.Sqlite;
using RocketMQ.Runner;

namespace RocketMQ.Runner.Unit.Tests;

public sealed class InMemoryMessageQueueStoreContractTests : MessageQueueStoreContractTests
{
    protected override Task<IMessageQueueStore> CreateStoreAsync()
        => Task.FromResult<IMessageQueueStore>(new InMemoryMessageQueueStore());
}

public sealed class InMemoryRoutingStoreContractTests : RoutingStoreContractTests
{
    protected override Task<IRoutingStore> CreateStoreAsync()
        => Task.FromResult<IRoutingStore>(new InMemoryRoutingStore());
}

public sealed class SqliteMessageQueueStoreContractTests : MessageQueueStoreContractTests
{
    private string? _databasePath;

    protected override async Task<IMessageQueueStore> CreateStoreAsync()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"rocketmq-contract-{Guid.NewGuid():N}.db");
        var database = new SqliteDatabase(
            $"Data Source={_databasePath};Mode=ReadWriteCreate;Cache=Shared;Pooling=False");
        var routingStore = new SqliteRoutingStore(database);
        await routingStore.DeclareQueueAsync(
            new QueueDefinition("test-queue", true, 10),
            CancellationToken.None);
        return new SqliteMessageQueueStore(database);
    }

    protected override Task DisposeStoreAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (_databasePath is not null)
        {
            DeleteIfExists(_databasePath);
            DeleteIfExists(_databasePath + "-wal");
            DeleteIfExists(_databasePath + "-shm");
        }

        return Task.CompletedTask;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
