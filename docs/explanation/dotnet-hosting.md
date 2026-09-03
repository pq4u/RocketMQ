# Hosting, DI i usługi tła w .NET

Runner używa Generic Host jako kontenera cyklu życia aplikacji. Host buduje konfigurację, rejestruje zależności, uruchamia serwer i usługi tła, obsługuje zamknięcie oraz zwalnia zasoby. Opis modelu znajduje się w [dokumentacji Generic Host](https://learn.microsoft.com/en-gb/dotnet/core/extensions/generic-host).

## Dependency injection

Rejestracja mapuje porty z Core na implementacje:

- <code>IRoutingStore</code> na <code>SqliteRoutingStore</code>,
- <code>IMessageQueueStore</code> na <code>SqliteMessageQueueStore</code>,
- <code>IMessagePublisher</code> na <code>SqliteMessagePublisher</code>.

Obiekty są singletonami, ponieważ współdzielą jedną bazę i kontrolę zapisu. Usługi gRPC pobierają porty w konstruktorach; nie tworzą adapterów samodzielnie. Jest to praktyczne zastosowanie dependency inversion. Więcej o kontenerze: [dependency injection w .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview).

## Composition root

<code>Program.cs</code> jest jedynym miejscem, które powinno znać konkretną konfigurację procesu: SQLite, port 50051, implementacje portów i hosted services. Dzięki temu Core pozostaje niezależny od hosta.

## BackgroundService i PeriodicTimer

<code>SqliteMaintenanceHostedService</code> dziedziczy po <code>BackgroundService</code>. Po uruchomieniu czyści stare dane, a następnie używa <code>PeriodicTimer</code> do powtarzania pracy co godzinę. Token hosta kończy pętlę przy shutdown.

Wyjątek nie powinien przypadkowo zatrzymać całego procesu; jednocześnie błędu nie wolno ukrywać. Obecna implementacja przechwytuje wyjątki utrzymania, ale nie rejestruje ich, co jest znanym ograniczeniem obserwowalności.

## Cykl życia

Host tworzy singletony przy pierwszym użyciu i zwalnia je podczas zamknięcia. Ma to znaczenie dla <code>IAsyncDisposable</code> publishera: kontrolowane zatrzymanie kończy kanał i czeka na worker. Nagłe przerwanie procesu nadal może utracić elementy, które nie dotarły do commit.

