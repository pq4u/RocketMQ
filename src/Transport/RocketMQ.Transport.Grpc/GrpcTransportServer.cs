using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RocketMQ.Core.Abstractions;
using RocketMQ.Transport.Grpc.Services;

namespace RocketMQ.Transport.Grpc;

public sealed class GrpcTransportServer : ITransportServer
{
    private readonly IMessagePublisher _publisher;
    private readonly IMessageQueueStore _queueStore;
    private readonly IRoutingStore _routingStore;
    private WebApplication? _app;

    public GrpcTransportServer(IMessagePublisher publisher, IMessageQueueStore queueStore, IRoutingStore routingStore)
    {
        _publisher = publisher;
        _queueStore = queueStore;
        _routingStore = routingStore;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(50051, listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(_publisher);
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
        if (_app is not null) await _app.StopAsync(cancellationToken);
    }

    public Task SendAsync(Guid connectionId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        => throw new InvalidOperationException("SendAsync is not supported in the unary gRPC transport.");
}
