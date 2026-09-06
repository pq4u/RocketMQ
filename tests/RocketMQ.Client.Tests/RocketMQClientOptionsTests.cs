using RocketMQ.Client;

namespace RocketMQ.Client.Tests;

public sealed class RocketMQClientOptionsTests
{
    [Fact]
    public void Endpoint_DefaultsToRunnerHttpsEndpoint()
    {
        var options = new RocketMQClientOptions();

        Assert.Equal("https://localhost:50051", options.Endpoint);
    }
}
