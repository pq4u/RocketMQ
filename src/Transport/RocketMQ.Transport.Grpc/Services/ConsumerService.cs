using Grpc.Core;
using RocketMQ.Core.Abstractions;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Transport.Grpc.Services;

public class ConsumerService : Consumer.ConsumerBase
{
    private readonly IMessageQueueStore _queueStore;

    public ConsumerService(IMessageQueueStore queueStore)
    {
        _queueStore = queueStore;
    }

    public override async Task<LeaseResponse> LeaseNext(LeaseRequest request, ServerCallContext context)
    {
        var leasedMessage = await _queueStore.LeaseNextAsync(
            request.QueueName, 
            TimeSpan.FromSeconds(request.VisibilityTimeoutSeconds), 
            context.CancellationToken);

        if (leasedMessage == null)
        {
            return new LeaseResponse();
        }

        return new LeaseResponse
        {
            LeaseId = leasedMessage.LeaseId.ToString(),
            Payload = Google.Protobuf.ByteString.CopyFrom(leasedMessage.Message.Payload.Span),
            DeliveryCount = leasedMessage.DeliveryCount,
            CorrelationId = leasedMessage.Message.CorrelationId.ToString()
        };
    }

    public override async Task<AckResponse> Ack(AckRequest request, ServerCallContext context)
    {
        await _queueStore.AckAsync(Guid.Parse(request.LeaseId), context.CancellationToken);
        return new AckResponse();
    }

    public override async Task<AckResponse> Nack(NackRequest request, ServerCallContext context)
    {
        await _queueStore.NackAsync(Guid.Parse(request.LeaseId), request.Requeue, context.CancellationToken);
        return new AckResponse();
    }
}
