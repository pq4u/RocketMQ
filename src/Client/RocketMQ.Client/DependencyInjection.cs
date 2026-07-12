using System;
using Microsoft.Extensions.DependencyInjection;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Client;

public class RocketMQClientOptions
{
    public string Endpoint { get; set; } = "https://localhost:5001";
}

public static class RocketMQClientExtensions
{
    public static IServiceCollection AddRocketMQClient(this IServiceCollection services, Action<RocketMQClientOptions> configureOptions)
    {
        var options = new RocketMQClientOptions();
        configureOptions(options);

        services.AddGrpcClient<RocketMQ.Transport.Grpc.Protos.Producer.ProducerClient>(o => { o.Address = new Uri(options.Endpoint); });
        services.AddGrpcClient<RocketMQ.Transport.Grpc.Protos.Consumer.ConsumerClient>(o => { o.Address = new Uri(options.Endpoint); });
        services.AddGrpcClient<RocketMQ.Transport.Grpc.Protos.Admin.AdminClient>(o => { o.Address = new Uri(options.Endpoint); });

        services.AddTransient<IProducer, Producer>();
        services.AddTransient<IConsumer, Consumer>();
        services.AddTransient<IAdminClient, AdminClient>();

        return services;
    }
}
