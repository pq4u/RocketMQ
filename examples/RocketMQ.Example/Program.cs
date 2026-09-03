using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RocketMQ.Client;

var services = new ServiceCollection();
services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
services.AddRocketMQClient(options =>
{
    options.Endpoint = "http://localhost:50051";
});

await using var serviceProvider = services.BuildServiceProvider();
var admin = serviceProvider.GetRequiredService<IAdminClient>();
var producer = serviceProvider.GetRequiredService<IProducer>();
await using var consumer = serviceProvider.GetRequiredService<IConsumer>();

var suffix = Guid.NewGuid().ToString("N");
var exchangeName = "docs.events." + suffix;
var queueName = "docs.orders." + suffix;

await admin.DeclareExchangeAsync(exchangeName, ExchangeType.Topic);
await admin.DeclareQueueAsync(queueName);
await admin.BindAsync(exchangeName, queueName, "orders.*");

var received = new TaskCompletionSource<string>(
    TaskCreationOptions.RunContinuationsAsynchronously);
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

await consumer.StartConsumingAsync(
    queueName,
    message =>
    {
        received.TrySetResult(Encoding.UTF8.GetString(message.Payload.Span));
        return Task.FromResult(ConsumeResult.Success);
    },
    timeout.Token);

var result = await producer.PublishAsync(
    exchangeName,
    "orders.created",
    Encoding.UTF8.GetBytes("order-123"),
    publishId: Guid.NewGuid(),
    ct: timeout.Token);

Console.WriteLine("Publikacja: " + result.Status + ", kolejki: " + result.DestinationQueues.Count);
Console.WriteLine("Odebrano: " + await received.Task.WaitAsync(timeout.Token));
Console.WriteLine("Komunikat został potwierdzony.");

