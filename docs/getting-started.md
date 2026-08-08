# Getting Started

## Run the broker locally

Install the .NET 10 SDK, then run from the repository root:

```powershell
dotnet restore
dotnet run --project src/Runner/RocketMQ.Runner
```

The local host exposes gRPC on `http://localhost:50051`. The endpoint is HTTP/2 without TLS, so use that exact `http` URI for local development. The SDK option default currently targets `https://localhost:5001`; override it explicitly.

## Use the .NET client

Add a project reference to `src/Client/RocketMQ.Client/RocketMQ.Client.csproj`, then register the SDK with dependency injection:

```csharp
services.AddRocketMQClient(options =>
{
    options.Endpoint = "http://localhost:50051";
});
```

Declare topology before publishing:

```csharp
var admin = serviceProvider.GetRequiredService<IAdminClient>();
await admin.DeclareExchangeAsync("events", ExchangeType.Topic);
await admin.DeclareQueueAsync("orders-worker");
await admin.BindAsync("events", "orders-worker", "orders.*");
```

Publish bytes with `IProducer`:

```csharp
var producer = serviceProvider.GetRequiredService<IProducer>();
await producer.PublishAsync(
    "events", "orders.created", Encoding.UTF8.GetBytes("order-123"));
```

Consume with `IConsumer`. Return `Success` to ack, `Requeue` to retry, or `DeadLetter` to reject permanently:

```csharp
await consumer.StartConsumingAsync("orders-worker", async message =>
{
    var body = Encoding.UTF8.GetString(message.Payload.Span);
    Console.WriteLine(body);
    return ConsumeResult.Success;
}, cancellationToken);
```

Dispose the consumer during shutdown so its lease loop stops cleanly.

## Raw gRPC contract

The services are `Producer`, `Consumer`, and `Admin`. The contract supports `Publish`, `LeaseNext`, `Ack`, `Nack`, `DeclareExchange`, `DeclareQueue`, and `Bind`. Use the `.proto` file as the source of truth when generating clients for other languages.

## Important limitations

The runner is process-local and loses messages and topology on restart. Persistent adapters are not implemented yet. Authentication, authorization, TLS configuration, clustering, metrics, and management endpoints are also outside the current prototype.
