using Grpc.Core;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Transport.Grpc.Services;

public class ProducerService : Producer.ProducerBase
{
    private readonly IMessageChannel<Envelope> _channel;

    public ProducerService(IMessageChannel<Envelope> channel)
    {
        _channel = channel;
    }

    public override async Task<PublishResponse> Publish(PublishRequest request, ServerCallContext context)
    {
        var connectionId = Guid.NewGuid();
        var correlationId = string.IsNullOrEmpty(request.CorrelationId) ? Guid.NewGuid() : Guid.Parse(request.CorrelationId);

        var inboundMessage = new InboundMessage(
            connectionId,
            correlationId,
            request.Payload.Memory,
            DateTimeOffset.UtcNow
        );

        var envelope = new Envelope(
            request.ExchangeName,
            request.RoutingKey,
            inboundMessage
        );

        try
        {
            using var cts = new CancellationTokenSource(0);
            await _channel.WriteAsync(envelope, cts.Token);
            return new PublishResponse { Success = true };
        }
        catch (OperationCanceledException)
        {
            throw new RpcException(new Status(StatusCode.ResourceExhausted, "The message channel is full (backpressure applied)."));
        }
    }
}
