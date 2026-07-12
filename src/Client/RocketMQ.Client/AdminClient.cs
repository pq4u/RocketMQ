using System.Threading.Tasks;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Client;

public class AdminClient : IAdminClient
{
    private readonly RocketMQ.Transport.Grpc.Protos.Admin.AdminClient _client;

    public AdminClient(RocketMQ.Transport.Grpc.Protos.Admin.AdminClient client)
    {
        _client = client;
    }

    public async Task DeclareExchangeAsync(string name, ExchangeType type)
    {
        var request = new DeclareExchangeRequest
        {
            ExchangeName = name ?? string.Empty,
            ExchangeType = type.ToString().ToLowerInvariant()
        };
        await _client.DeclareExchangeAsync(request);
    }

    public async Task DeclareQueueAsync(string name)
    {
        var request = new DeclareQueueRequest
        {
            QueueName = name ?? string.Empty
        };
        await _client.DeclareQueueAsync(request);
    }

    public async Task BindAsync(string exchangeName, string queueName, string routingKey)
    {
        var request = new BindRequest
        {
            ExchangeName = exchangeName ?? string.Empty,
            QueueName = queueName ?? string.Empty,
            RoutingKey = routingKey ?? string.Empty
        };
        await _client.BindAsync(request);
    }
}
