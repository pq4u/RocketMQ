# 3. Wymagania i architektura

## Wymagania funkcjonalne

System ma umożliwiać deklarowanie exchange, kolejek i bindingów, publikację z opcjonalnym kluczem idempotencji oraz odbiór przez lease zakończony Ack albo Nack. Routing ma obsługiwać dokładne dopasowanie, broadcast i wzorce topic.

## Wymagania jakościowe

Po zakończeniu operacji trwałego zapisu dane powinny przetrwać ponowne otwarcie store. Lease musi być atomowy przy konkurencji. Core nie może zależeć od technologii transportu ani magazynu. Kontrakty mają być weryfikowane wspólnymi testami adapterów.

## Podział warstw

~~~mermaid
flowchart TB
    Client[SDK .NET] --> Transport[gRPC adapter]
    Transport --> Core[Core: modele i porty]
    Sqlite[SQLite adapter] --> Core
    Runner[Runner: composition root] --> Transport
    Runner --> Sqlite
~~~

Zależności wskazują do środka. Core definiuje <code>IMessagePublisher</code>, <code>IMessageQueueStore</code>, <code>IRoutingStore</code> i <code>IMessageRouter</code>. Adaptery tłumaczą reprezentacje protobuf i SQL na model domenowy.

## Najważniejsza decyzja wykonawcza

Ścieżka publikacji nie składa się obecnie z ogólnego transportowego IMessageChannel i osobnego routera w tle. Usługa gRPC wywołuje trwały <code>IMessagePublisher</code>, a jego wewnętrzny bounded Channel grupuje żądania dla SQLite. Część starszych ADR opisuje wcześniejszy zamiar; [status funkcji](../reference/status-funkcji.md) odróżnia plan od implementacji.

## Własności graniczne

Odpowiedź Accepted następuje po commit. Wszystkie kolejki jednej publikacji są zapisywane atomowo. Konsument otrzymuje prawo do Ack przez unikalny LeaseId. Te granice są ważniejsze niż klasy i foldery, ponieważ określają zachowanie podczas awarii.

