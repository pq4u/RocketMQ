using Grpc.Core;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Transport.Grpc.Services;

public class AdminService : Admin.AdminBase
{
    private readonly IRoutingStore _routingStore;
    private readonly IBrokerRequestAuthorizer _authorizer;

    public AdminService(IRoutingStore routingStore)
        : this(routingStore, DisabledBrokerRequestAuthorizer.Instance)
    {
    }

    internal AdminService(
        IRoutingStore routingStore,
        IBrokerRequestAuthorizer authorizer)
    {
        _routingStore = routingStore;
        _authorizer = authorizer;
    }

    public override async Task<AdminResponse> DeclareExchange(DeclareExchangeRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ExchangeName))
        {
            throw InvalidArgument("Exchange name is required.");
        }

        _authorizer.Demand(
            context,
            BrokerPermission.Admin,
            BrokerResourceKind.Exchange,
            request.ExchangeName);
        var exchangeType = Enum.TryParse<ExchangeType>(request.ExchangeType, true, out var type) ? type : ExchangeType.Direct;
        var exchange = new Exchange(request.ExchangeName, exchangeType, true);
        await _routingStore.DeclareExchangeAsync(exchange, context.CancellationToken);
        return new AdminResponse { Success = true };
    }

    public override async Task<AdminResponse> DeclareQueue(DeclareQueueRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.QueueName))
        {
            throw InvalidArgument("Queue name is required.");
        }

        _authorizer.Demand(
            context,
            BrokerPermission.Admin,
            BrokerResourceKind.Queue,
            request.QueueName);
        var queue = new QueueDefinition(request.QueueName, true, 10);
        await _routingStore.DeclareQueueAsync(queue, context.CancellationToken);
        return new AdminResponse { Success = true };
    }

    public override async Task<AdminResponse> Bind(BindRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ExchangeName))
        {
            throw InvalidArgument("Exchange name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.QueueName))
        {
            throw InvalidArgument("Queue name is required.");
        }

        _authorizer.Demand(
            context,
            BrokerPermission.Admin,
            BrokerResourceKind.Exchange,
            request.ExchangeName);
        _authorizer.Demand(
            context,
            BrokerPermission.Admin,
            BrokerResourceKind.Queue,
            request.QueueName);
        var binding = new Binding(request.ExchangeName, request.QueueName, request.RoutingKey);
        await _routingStore.BindAsync(binding, context.CancellationToken);
        return new AdminResponse { Success = true };
    }

    private static RpcException InvalidArgument(string message)
        => new(new Status(StatusCode.InvalidArgument, message));
}
