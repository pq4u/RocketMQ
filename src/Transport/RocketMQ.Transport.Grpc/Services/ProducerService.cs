using Grpc.Core;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Transport.Grpc.Services;

public sealed class ProducerService : Producer.ProducerBase
{
    private readonly IMessagePublisher _publisher;

    public ProducerService(IMessagePublisher publisher) => _publisher = publisher;

    public override async Task<PublishResponse> Publish(PublishRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ExchangeName)) throw InvalidArgument("Exchange name is required.");
        if (!string.IsNullOrWhiteSpace(request.CorrelationId) && !Guid.TryParse(request.CorrelationId, out _)) throw InvalidArgument("Correlation ID must be a valid GUID.");
        if (!string.IsNullOrWhiteSpace(request.PublishId) && !Guid.TryParse(request.PublishId, out _)) throw InvalidArgument("Publish ID must be a valid GUID.");

        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid() : Guid.Parse(request.CorrelationId);
        var publishId = string.IsNullOrWhiteSpace(request.PublishId) ? Guid.NewGuid() : Guid.Parse(request.PublishId);
        var envelope = new Envelope(request.ExchangeName, request.RoutingKey, new InboundMessage(Guid.NewGuid(), correlationId, request.Payload.Memory, DateTimeOffset.UtcNow));
        try
        {
            var result = await _publisher.PublishAsync(publishId, envelope, context.CancellationToken);
            var response = new PublishResponse
            {
                Success = result.Status == PublishStatus.Accepted,
                MessageId = result.MessageId.ToString(),
                PublishId = result.PublishId.ToString(),
                Status = result.Status.ToString()
            };
            response.DestinationQueues.AddRange(result.DestinationQueues);
            return response;
        }
        catch (KeyNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Publish ID", StringComparison.Ordinal))
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, ex.Message));
        }
    }

    private static RpcException InvalidArgument(string message) => new(new Status(StatusCode.InvalidArgument, message));
}
