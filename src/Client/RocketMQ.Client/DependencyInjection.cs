using System;
using Microsoft.Extensions.DependencyInjection;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Client;

public class RocketMQClientOptions
{
    public string Endpoint { get; set; } = "https://localhost:50051";
    public string? ClientCertificatePath { get; set; }
    public string? ClientCertificateKeyPath { get; set; }
    public string? ClientCertificatePassword { get; set; }
}

public static class RocketMQClientExtensions
{
    public static IServiceCollection AddRocketMQClient(this IServiceCollection services, Action<RocketMQClientOptions> configureOptions)
    {
        var options = new RocketMQClientOptions();
        configureOptions(options);
        var clientCertificate = ClientCertificateLoader.Load(options);
        if (clientCertificate is not null)
        {
            services.AddSingleton(clientCertificate);
        }

        ConfigureGrpcClient(
            services.AddGrpcClient<RocketMQ.Transport.Grpc.Protos.Producer.ProducerClient>(
                o => o.Address = new Uri(options.Endpoint)),
            clientCertificate);
        ConfigureGrpcClient(
            services.AddGrpcClient<RocketMQ.Transport.Grpc.Protos.Consumer.ConsumerClient>(
                o => o.Address = new Uri(options.Endpoint)),
            clientCertificate);
        ConfigureGrpcClient(
            services.AddGrpcClient<RocketMQ.Transport.Grpc.Protos.Admin.AdminClient>(
                o => o.Address = new Uri(options.Endpoint)),
            clientCertificate);

        services.AddTransient<IProducer, Producer>();
        services.AddTransient<IConsumer, Consumer>();
        services.AddTransient<IAdminClient, AdminClient>();

        return services;
    }

    private static void ConfigureGrpcClient(
        IHttpClientBuilder clientBuilder,
        ClientCertificateHolder? clientCertificate)
    {
        if (clientCertificate is null)
        {
            return;
        }

        clientBuilder.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        {
            var certificate = serviceProvider.GetRequiredService<ClientCertificateHolder>().Certificate;
            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(certificate);
            return handler;
        });
    }
}
