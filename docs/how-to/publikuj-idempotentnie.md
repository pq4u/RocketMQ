# Publikuj komunikaty idempotentnie

Użyj stabilnego <code>publishId</code>, gdy klient może ponowić publikację po timeoutcie lub utracie odpowiedzi.

## Utwórz identyfikator operacji

Wygeneruj identyfikator raz i zachowaj go dla wszystkich prób tej samej logicznej publikacji:

~~~csharp
using System.Text;
using RocketMQ.Client;

var publishId = Guid.NewGuid();
var payload = Encoding.UTF8.GetBytes("order-123");

var first = await producer.PublishAsync(
    "events",
    "orders.created",
    payload,
    correlationId: null,
    publishId: publishId,
    ct: cancellationToken);

var retry = await producer.PublishAsync(
    "events",
    "orders.created",
    payload,
    correlationId: null,
    publishId: publishId,
    ct: cancellationToken);
~~~

Broker przechowuje wynik pod <code>publish_id</code> przez 24 godziny. Powtórzenie identycznego żądania zwraca pierwotny <code>message_id</code>, status i listę kolejek.

## Nie zmieniaj treści żądania

Ten sam <code>publishId</code> musi oznaczać tę samą wymianę, klucz routingu, correlation ID i payload. Zmiana danych powoduje błąd gRPC <code>AlreadyExists</code>.

## Obsłuż wynik routingu

Sprawdź właściwość <code>Accepted</code> albo tekstowy status:

~~~csharp
if (!first.Accepted)
{
    Console.WriteLine("Żadne wiązanie nie przyjęło komunikatu.");
}
~~~

Status <code>Unroutable</code> jest poprawną odpowiedzią protokołu, a nie wyjątkiem.

## Następne kroki

- [Sprawdź pola odpowiedzi Publish](../reference/grpc-api.md#producerpublish).
- [Zrozum transakcję publikacji](../explanation/publikacja-i-sqlite.md).
