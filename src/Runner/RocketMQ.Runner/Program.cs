using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Core.Routing;
using RocketMQ.Transport.Grpc;

namespace RocketMQ.Runner;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting RocketMQ Runner...");
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Core
                services.AddSingleton<IMessageQueueStore, InMemoryMessageQueueStore>();
                services.AddSingleton<IRoutingStore, InMemoryRoutingStore>();
                services.AddSingleton<IMessageRouter, MessageRouter>();
                services.AddSingleton<IMessageChannel<Envelope>, InMemoryMessageChannel>();
                services.AddSingleton<ITransportServer, GrpcTransportServer>();

                // Hosted Services
                services.AddHostedService<ServerHostedService>();
                services.AddHostedService<RoutingWorkerService>();
            })
            .Build();

        await host.RunAsync();
    }
}

public class InMemoryMessageChannel : IMessageChannel<Envelope>
{
    private readonly Channel<Envelope> _channel = Channel.CreateBounded<Envelope>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.Wait
    });

    public async ValueTask WriteAsync(Envelope message, CancellationToken cancellationToken)
    {
        if (_channel.Writer.TryWrite(message))
        {
            return;
        }

        try
        {
            await _channel.Writer.WriteAsync(message, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            throw new OperationCanceledException();
        }
    }

    public void Complete()
    {
        _channel.Writer.Complete();
    }

    public IAsyncEnumerable<Envelope> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}

public class RoutingWorkerService : BackgroundService
{
    private readonly IMessageChannel<Envelope> _channel;
    private readonly IMessageRouter _router;
    private readonly IMessageQueueStore _queueStore;

    public RoutingWorkerService(IMessageChannel<Envelope> channel, IMessageRouter router, IMessageQueueStore queueStore)
    {
        _channel = channel;
        _router = router;
        _queueStore = queueStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var envelope in _channel.ReadAllAsync(stoppingToken))
        {
            try
            {
                var queueNames = await _router.ResolveAsync(envelope.ExchangeName, envelope.RoutingKey, stoppingToken);
                foreach (var queue in queueNames)
                {
                    await _queueStore.EnqueueAsync(queue, envelope.Message, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Routing error: {ex.Message}");
            }
        }
    }
}

public class ServerHostedService : IHostedService
{
    private readonly ITransportServer _server;

    public ServerHostedService(ITransportServer server)
    {
        _server = server;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Starting GrpcTransportServer...");
        await _server.StartAsync(cancellationToken);
        Console.WriteLine("GrpcTransportServer started.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _server.StopAsync(cancellationToken);
    }
}
