using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Transport.Grpc.Services;

namespace RocketMQ.Transport.Grpc;

public sealed class GrpcTransportServer : ITransportServer
{
    private readonly IMessageChannel<Envelope> _inboundChannel;
    private readonly IMessageQueueStore _queueStore;
    private readonly IRoutingStore _routingStore;
    private WebApplication? _app;

    public GrpcTransportServer(
        IMessageChannel<Envelope> inboundChannel,
        IMessageQueueStore queueStore,
        IRoutingStore routingStore)
    {
        _inboundChannel = inboundChannel;
        _queueStore = queueStore;
        _routingStore = routingStore;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:50051"); // Default gRPC port
        
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(_inboundChannel);
        builder.Services.AddSingleton(_queueStore);
        builder.Services.AddSingleton(_routingStore);

        _app = builder.Build();
        _app.MapGrpcService<ProducerService>();
        _app.MapGrpcService<ConsumerService>();
        _app.MapGrpcService<AdminService>();

        await _app.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app != null)
        {
            await _app.StopAsync(cancellationToken);
        }
    }

    public Task SendAsync(Guid connectionId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("SendAsync is not supported in the unary gRPC transport.");
    }
}
