# 2. Podstawy techniczne

## Broker i competing consumers

Exchange rozdziela publikację do kolejek zgodnie z bindingami. Konsumenci konkurują o komunikaty w obrębie jednej kolejki, natomiast różne kolejki otrzymują własne kopie. Jest to model kolejki z potwierdzeniem, a nie append-only log z offsetami.

## At-least-once

Lease czasowo ukrywa komunikat. Brak Ack przed końcem visibility timeout przywraca możliwość dostarczenia. Eliminuje to trwałą utratę po awarii handlera kosztem możliwych duplikatów. Dlatego aplikacja powinna projektować idempotentne efekty.

## Asynchroniczność .NET

<code>Task</code> i <code>await</code> pozwalają nie blokować wątku podczas I/O. <code>Channel&lt;T&gt;</code> tworzy ograniczony bufor producent-konsument, a tryb Wait realizuje backpressure. Oficjalna dokumentacja opisuje [kanały i ich tryby zapełnienia](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels).

W projekcie Channel nie jest brokerową kolejką trwałą. Przenosi żądania od wielu wywołań Publish do pojedynczego workera SQLite. <code>TaskCompletionSource</code> wiąże zakończenie konkretnego RPC z commitem jego batcha, a <code>SemaphoreSlim</code> serializuje sekcję zapisu.

## Generic Host

Generic Host zarządza konfiguracją, dependency injection, usługami tła i shutdown. Jest composition rootem procesu zgodnie z [modelem hosta .NET](https://learn.microsoft.com/en-gb/dotnet/core/extensions/generic-host). Kontener DI dostarcza usługom gRPC porty zamiast konkretnych adapterów.

## gRPC i protobuf

gRPC generuje typowane klienty i bazowe klasy usług z kontraktu protobuf. Bieżące RPC są unary i działają po HTTP/2. [Podstawowe pojęcia gRPC](https://grpc.io/docs/what-is-grpc/core-concepts/) rozróżniają operacje unary i streaming. Stabilność numerów pól jest kluczowa dla ewolucji [proto3](https://protobuf.dev/programming-guides/proto3/).

## SQLite i WAL

SQLite jest wbudowanym magazynem pojedynczego pliku. Tryb WAL rozdziela główną bazę od dziennika zmian i pozwala czytelnikom współistnieć z writerem, ale zapis nadal serializuje się do jednego writera. Ograniczenia opisuje [dokumentacja SQLite WAL](https://sqlite.org/wal.html).

