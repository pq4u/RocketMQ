using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RocketMQ.Core.Abstractions;
using RocketMQ.Transport.Grpc.Services;

namespace RocketMQ.Transport.Grpc;

public sealed class GrpcTransportServer : ITransportServer
{
    private readonly IMessagePublisher _publisher;
    private readonly IMessageQueueStore _queueStore;
    private readonly IRoutingStore _routingStore;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment? _hostEnvironment;
    private WebApplication? _app;
    private ClientCertificateValidator? _clientCertificateValidator;

    public GrpcTransportServer(
        IMessagePublisher publisher,
        IMessageQueueStore queueStore,
        IRoutingStore routingStore,
        IConfiguration? configuration = null,
        IHostEnvironment? hostEnvironment = null)
    {
        _publisher = publisher;
        _queueStore = queueStore;
        _routingStore = routingStore;
        _configuration = configuration ?? new ConfigurationManager();
        _hostEnvironment = hostEnvironment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var transportConfiguration = CreateTransportConfiguration(_configuration);
        var mutualTlsEnabled = IsMutualTlsEnabled(transportConfiguration);
        var authorizationEnabled = ClientAuthorizationRegistry.IsEnabled(transportConfiguration);
        if (authorizationEnabled && !mutualTlsEnabled)
        {
            throw new InvalidOperationException(
                "RocketMQ:Security:Authorization:Enabled requires RocketMQ:Security:MutualTls:Enabled=true.");
        }

        ValidateEndpointConfiguration(transportConfiguration, mutualTlsEnabled);
        _clientCertificateValidator = mutualTlsEnabled
            ? ClientCertificateValidator.Load(transportConfiguration)
            : null;
        var clientAuthorizationRegistry = authorizationEnabled
            ? ClientAuthorizationRegistry.Load(transportConfiguration)
            : null;

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = _hostEnvironment?.EnvironmentName,
                ContentRootPath = _hostEnvironment?.ContentRootPath
            });
            builder.Configuration.AddConfiguration(transportConfiguration);
            builder.WebHost.ConfigureKestrel(options =>
            {
                if (_clientCertificateValidator is not null)
                {
                    options.ConfigureHttpsDefaults(httpsOptions =>
                    {
                        httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                        httpsOptions.ClientCertificateValidation = _clientCertificateValidator.Validate;
                    });
                }

                options.Configure(transportConfiguration.GetSection("Kestrel"));
            });
            builder.Services.AddGrpc();
            if (clientAuthorizationRegistry is not null)
            {
                builder.Services.AddSingleton(clientAuthorizationRegistry);
                builder.Services
                    .AddAuthentication(BrokerAuthenticationDefaults.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, ClientCertificateAuthenticationHandler>(
                        BrokerAuthenticationDefaults.Scheme,
                        _ => { });
                builder.Services.AddAuthorization(options =>
                {
                    AddPermissionPolicy(
                        options,
                        BrokerAuthorizationPolicies.Publish,
                        BrokerPermission.Publish);
                    AddPermissionPolicy(
                        options,
                        BrokerAuthorizationPolicies.Consume,
                        BrokerPermission.Consume);
                    AddPermissionPolicy(
                        options,
                        BrokerAuthorizationPolicies.Admin,
                        BrokerPermission.Admin);
                });
                builder.Services.AddSingleton<IAuthorizationHandler, BrokerPermissionAuthorizationHandler>();
                builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, BrokerAuthorizationResultHandler>();
            }

            builder.Services.AddSingleton(_publisher);
            builder.Services.AddSingleton(_queueStore);
            builder.Services.AddSingleton(_routingStore);
            _app = builder.Build();
            if (clientAuthorizationRegistry is not null)
            {
                _app.UseAuthentication();
                _app.UseAuthorization();
                _app.MapGrpcService<ProducerService>()
                    .RequireAuthorization(BrokerAuthorizationPolicies.Publish);
                _app.MapGrpcService<ConsumerService>()
                    .RequireAuthorization(BrokerAuthorizationPolicies.Consume);
                _app.MapGrpcService<AdminService>()
                    .RequireAuthorization(BrokerAuthorizationPolicies.Admin);
            }
            else
            {
                _app.MapGrpcService<ProducerService>();
                _app.MapGrpcService<ConsumerService>();
                _app.MapGrpcService<AdminService>();
            }

            await _app.StartAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await DisposeAppAsync();
            }
            finally
            {
                DisposeClientCertificateValidator();
            }

            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_app is not null)
            {
                await _app.StopAsync(cancellationToken);
            }
        }
        finally
        {
            try
            {
                await DisposeAppAsync();
            }
            finally
            {
                DisposeClientCertificateValidator();
            }
        }
    }

    public Task SendAsync(Guid connectionId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        => throw new InvalidOperationException("SendAsync is not supported in the unary gRPC transport.");

    private static IConfiguration CreateTransportConfiguration(IConfiguration configuration)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kestrel:Endpoints:Grpc:Url"] = "https://localhost:50051",
                ["Kestrel:Endpoints:Grpc:Protocols"] = "Http2",
                ["RocketMQ:Security:MutualTls:Enabled"] = "false",
                ["RocketMQ:Security:MutualTls:RevocationMode"] = "NoCheck",
                ["RocketMQ:Security:Authorization:Enabled"] = "false"
            })
            .AddConfiguration(configuration)
            .Build();

    private static void ValidateEndpointConfiguration(IConfiguration configuration, bool mutualTlsEnabled)
    {
        var endpoints = configuration.GetSection("Kestrel:Endpoints").GetChildren().ToArray();
        if (!endpoints.Any(endpoint => string.Equals(endpoint.Key, "Grpc", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Configure the Kestrel:Endpoints:Grpc endpoint.");
        }

        foreach (var endpoint in endpoints)
        {
            var url = endpoint["Url"];
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException($"Configure Kestrel:Endpoints:{endpoint.Key}:Url with an absolute HTTP(S) URI.");
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || !uri.IsLoopback))
            {
                throw new InvalidOperationException(
                    $"Kestrel endpoint '{endpoint.Key}' must use HTTPS. Insecure HTTP is allowed only on a loopback address.");
            }

            if (mutualTlsEnabled && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Kestrel endpoint '{endpoint.Key}' must use HTTPS when mutual TLS is enabled.");
            }

            var configuredClientCertificateMode = endpoint["ClientCertificateMode"]
                ?? configuration["Kestrel:EndpointDefaults:ClientCertificateMode"];
            if (mutualTlsEnabled
                && configuredClientCertificateMode is not null
                && !string.Equals(
                    configuredClientCertificateMode,
                    nameof(ClientCertificateMode.RequireCertificate),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Kestrel endpoint '{endpoint.Key}' must use ClientCertificateMode RequireCertificate when mutual TLS is enabled.");
            }

            var protocols = endpoint["Protocols"] ?? configuration["Kestrel:EndpointDefaults:Protocols"];
            if (!string.Equals(protocols, "Http2", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Kestrel endpoint '{endpoint.Key}' must use the Http2 protocol.");
            }
        }
    }

    private static bool IsMutualTlsEnabled(IConfiguration configuration)
    {
        var value = configuration["RocketMQ:Security:MutualTls:Enabled"];
        if (!bool.TryParse(value, out var enabled))
        {
            throw new InvalidOperationException(
                "RocketMQ:Security:MutualTls:Enabled must be true or false.");
        }

        return enabled;
    }

    private static void AddPermissionPolicy(
        AuthorizationOptions options,
        string policyName,
        BrokerPermission permission)
    {
        options.AddPolicy(
            policyName,
            policy =>
            {
                policy.AddAuthenticationSchemes(BrokerAuthenticationDefaults.Scheme);
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new BrokerPermissionRequirement(permission));
            });
    }

    private async Task DisposeAppAsync()
    {
        if (_app is null)
        {
            return;
        }

        await _app.DisposeAsync();
        _app = null;
    }

    private void DisposeClientCertificateValidator()
    {
        _clientCertificateValidator?.Dispose();
        _clientCertificateValidator = null;
    }
}
