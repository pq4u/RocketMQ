using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RocketMQ.Core.Abstractions;
using RocketMQ.Persistence.Sqlite;
using RocketMQ.Transport.Grpc;

namespace RocketMQ.Runner;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                var databasePath = context.Configuration["RocketMQ:Persistence:DatabasePath"];
                if (string.IsNullOrWhiteSpace(databasePath)
                    || !Path.IsPathFullyQualified(databasePath)
                    || databasePath.StartsWith("\\\\", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Configure RocketMQ:Persistence:DatabasePath with an absolute local path, for example --RocketMQ:Persistence:DatabasePath=C:\\RocketMQData\\rocketmq.db.");
                }

                var directory = Path.GetDirectoryName(databasePath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    throw new InvalidOperationException("The configured SQLite database path must include a directory.");
                }

                Directory.CreateDirectory(directory);

                var connectionString = $"Data Source={databasePath};Mode=ReadWriteCreate;Cache=Shared";
                services.AddSingleton(new SqliteDatabase(connectionString));
                services.AddSingleton<IMessageQueueStore, SqliteMessageQueueStore>();
                services.AddSingleton<IRoutingStore, SqliteRoutingStore>();
                services.AddSingleton<IPersistenceStore, SqlitePersistenceStore>();
                services.AddSingleton<IMessagePublisher, SqliteMessagePublisher>();
                services.AddSingleton<ITransportServer, GrpcTransportServer>();
                services.AddSingleton<SqliteMaintenanceService>();
                services.AddHostedService<ServerHostedService>();
                services.AddHostedService<SqliteMaintenanceHostedService>();
            })
            .Build();

        await host.RunAsync();
    }
}

public sealed class ServerHostedService : IHostedService
{
    private readonly ITransportServer _server;

    public ServerHostedService(ITransportServer server) => _server = server;
    public Task StartAsync(CancellationToken cancellationToken) => _server.StartAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => _server.StopAsync(cancellationToken);
}

public sealed class SqliteMaintenanceHostedService : BackgroundService
{
    private readonly SqliteMaintenanceService _maintenance;

    public SqliteMaintenanceHostedService(SqliteMaintenanceService maintenance) => _maintenance = maintenance;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            await _maintenance.PurgeDeadLettersAsync(TimeSpan.FromDays(30), stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
