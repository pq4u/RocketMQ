using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using RocketMQ.Transport.Grpc.Protos;
using Microsoft.Extensions.Logging;

namespace RocketMQ.Client;

public class Producer : IProducer
{
    private readonly RocketMQ.Transport.Grpc.Protos.Producer.ProducerClient _client;
    private readonly ILogger<Producer> _logger;

    public Producer(RocketMQ.Transport.Grpc.Protos.Producer.ProducerClient client, ILogger<Producer> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task PublishAsync(string exchangeName, string routingKey, ReadOnlyMemory<byte> payload, string? correlationId = null, CancellationToken ct = default)
    {
        var request = new PublishRequest
        {
            ExchangeName = exchangeName ?? string.Empty,
            RoutingKey = routingKey ?? string.Empty,
            Payload = Google.Protobuf.ByteString.CopyFrom(payload.Span),
            CorrelationId = correlationId ?? string.Empty
        };

        int maxRetries = 5;
        int delayMs = 100;
        var random = new Random();

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _client.PublishAsync(request, cancellationToken: ct);
                return;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.ResourceExhausted)
            {
                if (attempt == maxRetries)
                {
                    _logger.LogError(ex, "Max retries reached when publishing to {ExchangeName}. Resource exhausted.", exchangeName);
                    throw;
                }

                int jitter = random.Next(0, 50);
                var waitTime = TimeSpan.FromMilliseconds(delayMs + jitter);
                _logger.LogWarning("Resource exhausted. Retrying {Attempt}/{MaxRetries} in {WaitTimeMs}ms...", attempt + 1, maxRetries, waitTime.TotalMilliseconds);
                await Task.Delay(waitTime, ct);
                delayMs *= 2; // exponential backoff
            }
        }
    }
}
