# Referencja SDK .NET

Rejestracja:

~~~csharp
services.AddRocketMQClient(options =>
{
    options.Endpoint = "https://localhost:50051";
});
~~~

Domyślna wartość SDK to <code>https://localhost:50051</code> i odpowiada lokalnemu Runnerowi. Dla innego wdrożenia podaj endpoint jawnie. SDK używa standardowej walidacji certyfikatu systemu operacyjnego i nie wyłącza sprawdzania nazwy hosta ani łańcucha zaufania.

## Certyfikat klienta mTLS

Dla PFX/P12 ustaw ścieżkę i hasło pochodzące ze źródła sekretów:

~~~csharp
services.AddRocketMQClient(options =>
{
    options.Endpoint = "https://broker.example:50051";
    options.ClientCertificatePath = configuration["RocketMQ:ClientCertificatePath"];
    options.ClientCertificatePassword = configuration["RocketMQ:ClientCertificatePassword"];
});
~~~

Dla PEM/CRT ustaw również <code>ClientCertificateKeyPath</code>. Certyfikat musi zawierać lub wskazywać klucz prywatny, a endpoint musi używać HTTPS. SDK ładuje certyfikat podczas rejestracji DI i dodaje go do handlerów Producer, Consumer i Admin. Połączenia HTTP/2 są ponownie używane; zmiana certyfikatu wymaga restartu aplikacji.

SDK nadal waliduje certyfikat serwera przy użyciu systemowego magazynu zaufania. Konfiguracja certyfikatu klienta nie wyłącza sprawdzania serwera.

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
