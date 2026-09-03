# Referencja SDK .NET

Rejestracja:

~~~csharp
services.AddRocketMQClient(options =>
{
    options.Endpoint = "http://localhost:50051";
});
~~~

Jawne podanie endpointu jest zalecane. Domyślna wartość w bieżącym SDK to <code>https://localhost:5001</code> i nie odpowiada lokalnemu Runnerowi.

## IProducer

<code>PublishAsync(exchangeName, routingKey, payload, correlationId, publishId, ct)</code> zwraca <code>PublishResult</code>: PublishId, MessageId, Status, DestinationQueues i właściwość Accepted.

SDK generuje PublishId, jeśli go nie podano. Dla <code>ResourceExhausted</code> ponawia wywołanie do pięciu razy z wykładniczym opóźnieniem. Obecny serwer nie generuje tego statusu na ścieżce pełnego kanału.

## IAdminClient

- <code>DeclareExchangeAsync(name, ExchangeType)</code>
- <code>DeclareQueueAsync(name)</code>
- <code>BindAsync(exchangeName, queueName, routingKey)</code>

DeclareQueue tworzy w bieżącym serwerze trwałą kolejkę z limitem 10 dzierżaw. SDK nie udostępnia ustawienia tego limitu.

## IConsumer

<code>StartConsumingAsync</code> przyjmuje nazwę kolejki, asynchroniczny handler i opcjonalne <code>ConsumerOptions</code>. Handler zwraca:

| ConsumeResult | Operacja |
|---|---|
| <code>Success</code> | Ack |
| <code>Requeue</code> | Nack z requeue=true |
| <code>DeadLetter</code> | Nack z requeue=false |

SDK przetwarza po jednym komunikacie, czeka 1 sekundę po pustej odpowiedzi i 2 sekundy po błędzie. <code>ConsumerOptions.VisibilityTimeout</code> ma domyślnie 30 sekund i zakres 1 sekunda–1 godzina. <code>IConsumer</code> należy zwolnić asynchronicznie.
