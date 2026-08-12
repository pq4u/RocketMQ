using Grpc.Core;
using Microsoft.Extensions.Logging;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Client;

public sealed class Producer : IProducer
{
    private readonly RocketMQ.Transport.Grpc.Protos.Producer.ProducerClient _client;
    private readonly ILogger<Producer> _logger;

    public Producer(RocketMQ.Transport.Grpc.Protos.Producer.ProducerClient client, ILogger<Producer> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<PublishResult> PublishAsync(string exchangeName, string routingKey, ReadOnlyMemory<byte> payload, string? correlationId = null, Guid? publishId = null, CancellationToken ct = default)
    {
        var request = new PublishRequest
        {
            ExchangeName = exchangeName ?? string.Empty,
            RoutingKey = routingKey ?? string.Empty,
            Payload = Google.Protobuf.ByteString.CopyFrom(payload.Span),
            CorrelationId = correlationId ?? string.Empty,
            PublishId = (publishId ?? Guid.NewGuid()).ToString()
        };
        var delay = TimeSpan.FromMilliseconds(100);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await _client.PublishAsync(request, cancellationToken: ct);
                return new PublishResult(Guid.Parse(response.PublishId), Guid.Parse(response.MessageId), response.Status, response.DestinationQueues.ToList());
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.ResourceExhausted && attempt < 5)
            {
                _logger.LogWarning(ex, "Publish backpressured; retrying attempt {Attempt}.", attempt + 1);
                await Task.Delay(delay, ct);
                delay *= 2;
            }
        }
    }
}
