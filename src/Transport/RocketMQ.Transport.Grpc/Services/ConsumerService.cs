using Grpc.Core;
using RocketMQ.Core.Abstractions;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Transport.Grpc.Services;

public class ConsumerService : Consumer.ConsumerBase
{
    private const int MinimumVisibilityTimeoutSeconds = 1;
    private const int MaximumVisibilityTimeoutSeconds = 3600;

    private readonly IMessageQueueStore _queueStore;
    private readonly IBrokerRequestAuthorizer _authorizer;

    public ConsumerService(IMessageQueueStore queueStore)
        : this(queueStore, DisabledBrokerRequestAuthorizer.Instance)
    {
    }

    internal ConsumerService(
        IMessageQueueStore queueStore,
        IBrokerRequestAuthorizer authorizer)
    {
        _queueStore = queueStore;
        _authorizer = authorizer;
    }

    public override async Task<LeaseResponse> LeaseNext(LeaseRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.QueueName))
        {
            throw InvalidArgument("Queue name is required.");
        }

        if (request.VisibilityTimeoutSeconds is < MinimumVisibilityTimeoutSeconds
            or > MaximumVisibilityTimeoutSeconds)
        {
            throw InvalidArgument(
                $"Visibility timeout must be between {MinimumVisibilityTimeoutSeconds} " +
                $"and {MaximumVisibilityTimeoutSeconds} seconds.");
        }

        _authorizer.Demand(
            context,
            BrokerPermission.Consume,
            BrokerResourceKind.Queue,
            request.QueueName);
        var leaseOwnerId = _authorizer.GetLeaseOwnerId(context);

        try
        {
            var leasedMessage = await _queueStore.LeaseNextAsync(
                request.QueueName,
                TimeSpan.FromSeconds(request.VisibilityTimeoutSeconds),
                leaseOwnerId,
                context.CancellationToken);

            if (leasedMessage == null)
            {
                return new LeaseResponse();
            }

            return new LeaseResponse
            {
                LeaseId = leasedMessage.LeaseId.ToString(),
                MessageId = leasedMessage.MessageId.ToString(),
                Payload = Google.Protobuf.ByteString.CopyFrom(leasedMessage.Message.Payload.Span),
                DeliveryCount = leasedMessage.DeliveryCount,
                CorrelationId = leasedMessage.Message.CorrelationId.ToString()
            };
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw InvalidArgument(ex.Message);
        }
    }

    public override async Task<AckResponse> Ack(AckRequest request, ServerCallContext context)
    {
        var leaseId = ParseLeaseId(request.LeaseId);

        try
        {
            var leaseOwnerId = _authorizer.GetLeaseOwnerId(context);
            var expectedQueueName = await ResolveExpectedQueueNameAsync(
                request.QueueName,
                leaseId,
                leaseOwnerId,
                context);
            await _queueStore.AckAsync(
                leaseId,
                leaseOwnerId,
                expectedQueueName,
                context.CancellationToken);
            return new AckResponse();
        }
        catch (InvalidOperationException ex)
        {
            throw MapLeaseError(ex);
        }
    }

    public override async Task<AckResponse> Nack(NackRequest request, ServerCallContext context)
    {
        var leaseId = ParseLeaseId(request.LeaseId);

        try
        {
            var leaseOwnerId = _authorizer.GetLeaseOwnerId(context);
            var expectedQueueName = await ResolveExpectedQueueNameAsync(
                request.QueueName,
                leaseId,
                leaseOwnerId,
                context);
            await _queueStore.NackAsync(
                leaseId,
                request.Requeue,
                leaseOwnerId,
                expectedQueueName,
                context.CancellationToken);
            return new AckResponse();
        }
        catch (InvalidOperationException ex)
        {
            throw MapLeaseError(ex);
        }
    }

    private async Task<string?> ResolveExpectedQueueNameAsync(
        string configuredQueueName,
        Guid leaseId,
        string? leaseOwnerId,
        ServerCallContext context)
    {
        if (configuredQueueName.Length > 0 && string.IsNullOrWhiteSpace(configuredQueueName))
        {
            throw InvalidArgument("Queue name must not contain only whitespace.");
        }

        var expectedQueueName = configuredQueueName.Length == 0
            && _authorizer.RequiresResourceName(context, BrokerPermission.Consume)
                ? await _queueStore.GetActiveLeaseQueueAsync(
                    leaseId,
                    leaseOwnerId,
                    context.CancellationToken)
                : configuredQueueName.Length == 0 ? null : configuredQueueName;
        if (expectedQueueName is not null)
        {
            _authorizer.Demand(
                context,
                BrokerPermission.Consume,
                BrokerResourceKind.Queue,
                expectedQueueName);
        }

        return expectedQueueName;
    }

    private static Guid ParseLeaseId(string value)
    {
        if (!Guid.TryParse(value, out var leaseId))
        {
            throw InvalidArgument("Lease ID must be a valid GUID.");
        }

        return leaseId;
    }

    private static RpcException MapLeaseError(InvalidOperationException exception)
        => exception.Message.Contains("expired", StringComparison.OrdinalIgnoreCase)
            ? new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message))
            : new RpcException(new Status(StatusCode.NotFound, exception.Message));

    private static RpcException InvalidArgument(string message)
        => new(new Status(StatusCode.InvalidArgument, message));
}
