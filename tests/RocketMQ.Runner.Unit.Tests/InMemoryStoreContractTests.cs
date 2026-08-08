using RocketMQ.Contract.Tests;
using RocketMQ.Core.Abstractions;
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