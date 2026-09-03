# Zaimplementuj konsumenta

Ta instrukcja tworzy konsumenta SDK, który odpytuje kolejkę, wywołuje handler i potwierdza wynik.

## Zarejestruj SDK

Zarejestruj klientów w kontenerze dependency injection:

~~~csharp
using Microsoft.Extensions.DependencyInjection;
using RocketMQ.Client;

services.AddRocketMQClient(options =>
{
    options.Endpoint = "http://localhost:50051";
});
~~~

Adres musi wskazywać działający endpoint gRPC. Bieżący domyślny adres SDK nie odpowiada endpointowi runnera, dlatego ustaw go jawnie.

## Uruchom konsumenta

Rozwiąż <code>IConsumer</code>, ustaw czas widoczności i uruchom handler:

~~~csharp
using System.Text;
using RocketMQ.Client;

await using var consumer = serviceProvider.GetRequiredService<IConsumer>();
var options = new ConsumerOptions
{
    VisibilityTimeout = TimeSpan.FromSeconds(30)
};

await consumer.StartConsumingAsync(
    "orders-worker",
    message =>
    {
        var body = Encoding.UTF8.GetString(message.Payload.Span);
        Console.WriteLine(message.MessageId + ": " + body);
        return Task.FromResult(ConsumeResult.Success);
    },
    options,
    cancellationToken);
~~~

<code>Success</code> wysyła <code>Ack</code>, <code>Requeue</code> wysyła <code>Nack</code> z ponownym kolejkowaniem, a <code>DeadLetter</code> odrzuca komunikat bez ponownego kolejkowania.

## Zamknij konsumenta

Wywołaj <code>DisposeAsync</code>, na przykład przez <code>await using</code>. SDK anuluje pętlę w tle i czeka na jej zakończenie.

## Następne kroki

- [Obsłuż ponowne dostarczenie](obsluz-redelivery.md).
- [Sprawdź opcje konsumenta](../reference/dotnet-sdk.md#consumeroptions).
