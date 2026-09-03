# Referencja portów Core

Core nie zależy od gRPC ani SQLite. Jego publiczne porty określają zachowanie adapterów.

## Publikacja i routing

- <code>IMessagePublisher.PublishAsync</code> — trwała, routowana publikacja z PublishId.
- <code>IMessageRouter.RouteAsync</code> — wyznaczenie unikalnych kolejek.
- <code>IRoutingStore</code> — deklarowanie, usuwanie i odczyt exchange, kolejek i bindingów.

Deklaracje są idempotentne dla zgodnej konfiguracji. Usunięcie nieistniejącego obiektu jest no-op. Binding wymaga istniejącego exchange i kolejki.

## Kolejka

| Metoda | Kontrakt |
|---|---|
| <code>EnqueueAsync</code> | trwały zapis i MessageId |
| <code>LeaseNextAsync</code> | atomowy lease najstarszej dostępnej wiadomości lub null |
| <code>AckAsync</code> | usunięcie przy aktywnym lease |
| <code>NackAsync</code> | requeue albo dead-letter przy aktywnym lease |
| <code>BrowseDeadLettersAsync</code> | asynchroniczny, tylko do odczytu przegląd dead letters |

Metody store muszą być bezpieczne współbieżnie. Kontrakty są opisane w [abstrakcjach Core](../../src/Core/RocketMQ.Core/Abstractions/) i egzekwowane przez wspólne testy.

<code>IMessageChannel&lt;T&gt;</code> oraz <code>ITransportServer</code> pozostają w kodzie, lecz bieżąca ścieżka publikacji Runnera używa <code>IMessagePublisher</code>, nie ogólnego IMessageChannel.

