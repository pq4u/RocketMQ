# 4. Client SDK Architecture

Date: 2026-07-12

## Status

Proposed

## Context

Zaimplementowaliśmy bazową architekturę kolejkowania (ADR-0001), rutingu (ADR-0002) oraz warstwę sieciową opartą na gRPC (ADR-0003). Protokoły gRPC (protobuf) definiują ścisły kontrakt, jednak bezpośrednie korzystanie z wygenerowanych klas klienckich gRPC wymagałoby od programistów pisania powtarzalnego kodu (tzw. boilerplate) do obsługi m.in.:
1. **Backpressure producenta**: obsługa błędów `RESOURCE_EXHAUSTED` i implementacja mechanizmu *retry* z *exponential backoff*.
2. **Pętli konsumenta**: nieustannego odpytywania (polling) metody `LeaseNext` i zarządzania odpowiedziami `Ack` / `Nack`.
3. **Zarządzania połączeniem**: obsługi kanału gRPC (`GrpcChannel`) i konfiguracji TLS.

Potrzebujemy wysokopoziomowego Client SDK, które ukryje te detale implementacyjne i zapewni płynne, idiomatyczne doświadczenie programistyczne (DX - Developer Experience) dla użytkowników końcowych naszej platformy.

## Decision

Utworzymy dedykowany projekt biblioteki klienckiej: **`RocketMQ.Client`**, który będzie korzystał bezpośrednio ze skompilowanych kontraktów gRPC, oferując wysokopoziomowe fasady.

### 1. Interfejs Producenta (Producer)

Producent dostarczy metodę asynchroniczną do publikacji wiadomości do konkretnego *exchange*.
Kluczową funkcjonalnością będzie wbudowana, transparentna obsługa zjawiska **backpressure**. Jeśli serwer gRPC zwróci status `RESOURCE_EXHAUSTED`, SDK automatycznie ponowi próbę wysyłki wykorzystując algorytm *exponential backoff* z mechanizmem *jitter*, aby uniknąć problemu "thundering herd".

```csharp
public interface IProducer
{
    Task PublishAsync(string exchangeName, string routingKey, ReadOnlyMemory<byte> payload, string? correlationId = null, CancellationToken ct = default);
}
```

### 2. Interfejs Konsumenta (Consumer)

Konsument przejmie na siebie ukrycie mechanizmu pull (ciągłego wywoływania `LeaseNext`). Udostępni zdarzeniowy (event-driven) model subskrypcji w modelu push dla użytkownika SDK.

Programista zarejestruje funkcję obsługi (handler), a SDK utworzy wewnętrzną pętlę w tle (background worker), która będzie:
1. Asynchronicznie wywoływać `LeaseNext`.
2. Przekazywać wiadomość do handlera programisty.
3. W zależności od rezultatu (powodzenie lub rzucenie wyjątku), wywoływać pod spodem `Ack` lub `Nack` (requeue/dead-letter).

```csharp
public enum ConsumeResult { Success, Requeue, DeadLetter }

public interface IConsumer : IAsyncDisposable
{
    Task StartConsumingAsync(string queueName, Func<MessageContext, Task<ConsumeResult>> handler, CancellationToken ct = default);
}
```

### 3. Zarządzanie konfiguracją i cyklem życia

Dostarczymy punkt wejścia w postaci fabryki `RocketMQClientFactory` lub rozszerzeń do Dependency Injection (np. `AddRocketMQClient()`), w którym konfiguruje się adresy endpointów gRPC.

### 4. Administracja (Admin)

SDK zaoferuje klienta do zarządzania topologią:
```csharp
public interface IAdminClient
{
    Task DeclareExchangeAsync(string name, ExchangeType type);
    Task DeclareQueueAsync(string name);
    Task BindAsync(string exchangeName, string queueName, string routingKey);
}
```

## Consequences

### Positive
- **Developer Experience**: radykalne ułatwienie dla deweloperów. Piszą oni biznesowy handler, nie martwiąc się o techniczne szczegóły komunikacji sieciowej i mechanizmu dzierżaw (leases).
- **Stabilność platformy**: Wbudowanie mechanizmu ponowień (`exponential backoff`) chroni klaster RocketMQ przed zalaniem ruchem, jednocześnie zapobiegając bezpowrotnej utracie danych podczas chwilowych opóźnień wewnętrznych serwera (backpressure).
- **Enkapsulacja infrastruktury**: łatwiejsza aktualizacja warstwy gRPC w przyszłości – użytkownicy zależą tylko od paczki `.Client`.

### Negative
- Dodatkowy projekt do utrzymywania: musimy aktualizować klienta wraz z ewentualnymi zmianami w `rocketmq.proto`.
- Asynchroniczna pętla konsumencka (polling `LeaseNext`) musi być ostrożnie zaprojektowana by unikać wycieków pamięci i prawidłowo zarządzać stanem `CancellationToken`. Współbieżność u klienta będzie wymagała rygorystycznych testów jednostkowych.
