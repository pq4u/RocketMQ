using Grpc.Core;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Transport.Grpc.Services;

public class AdminService : Admin.AdminBase
{
    private readonly IRoutingStore _routingStore;

    public AdminService(IRoutingStore routingStore)
    {
        _routingStore = routingStore;
    }

    public override async Task<AdminResponse> DeclareExchange(DeclareExchangeRequest request, ServerCallContext context)
    {
        var exchangeType = Enum.TryParse<ExchangeType>(request.ExchangeType, true, out var type) ? type : ExchangeType.Direct;
        var exchange = new Exchange(request.ExchangeName, exchangeType, true);
        await _routingStore.DeclareExchangeAsync(exchange, context.CancellationToken);
        return new AdminResponse { Success = true };
    }

    public override async Task<AdminResponse> DeclareQueue(DeclareQueueRequest request, ServerCallContext context)
    {
        var queue = new QueueDefinition(request.QueueName, true, 10);
        await _routingStore.DeclareQueueAsync(queue, context.CancellationToken);
        return new AdminResponse { Success = true };
    }

    public override async Task<AdminResponse> Bind(BindRequest request, ServerCallContext context)
    {
        var binding = new Binding(request.ExchangeName, request.QueueName, request.RoutingKey);
        await _routingStore.BindAsync(binding, context.CancellationToken);
        return new AdminResponse { Success = true };
    }
}
