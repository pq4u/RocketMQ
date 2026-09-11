# Hosting, DI i usługi tła w .NET

Runner używa Generic Host jako kontenera cyklu życia aplikacji. Host buduje konfigurację, rejestruje zależności, uruchamia serwer i usługi tła, obsługuje zamknięcie oraz zwalnia zasoby. Opis modelu znajduje się w [dokumentacji Generic Host](https://learn.microsoft.com/en-gb/dotnet/core/extensions/generic-host).

## Dependency injection

Rejestracja mapuje porty z Core na implementacje:

- <code>IRoutingStore</code> na <code>SqliteRoutingStore</code>,
- <code>IMessageQueueStore</code> na <code>SqliteMessageQueueStore</code>,
- <code>IMessagePublisher</code> na <code>SqliteMessagePublisher</code>.

Obiekty są singletonami, ponieważ współdzielą jedną bazę i kontrolę zapisu. Usługi gRPC pobierają porty w konstruktorach; nie tworzą adapterów samodzielnie. Jest to praktyczne zastosowanie dependency inversion. Więcej o kontenerze: [dependency injection w .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview).

## Composition root

<code>Program.cs</code> jest composition rootem dla SQLite, implementacji portów i hosted services. <code>GrpcTransportServer</code> pobiera z głównego hosta standardową sekcję Kestrela, która określa endpoint HTTP/2 i certyfikat TLS. Domyślny adres to <code>https://localhost:50051</code>. Opcjonalna sekcja <code>RocketMQ:Security:MutualTls</code> włącza żądanie certyfikatu klienta i wskazuje prywatne CA używane przez callback Kestrela. Opcjonalna sekcja <code>Authorization</code> ładuje snapshot allowlisty, tworzy principal z certyfikatu, chroni grupy endpointów politykami ASP.NET Core i w serwisach sprawdza dokładną nazwę exchange lub kolejki. Walidator, rejestr ACL oraz certyfikaty CA żyją tak długo jak wewnętrzny host gRPC. Core pozostaje niezależny od hosta, TLS i autoryzacji; zna jedynie opcjonalny tekstowy identyfikator właściciela lease'a.

## BackgroundService i PeriodicTimer

<code>SqliteMaintenanceHostedService</code> dziedziczy po <code>BackgroundService</code>. Po uruchomieniu czyści stare dane, a następnie używa <code>PeriodicTimer</code> do powtarzania pracy co godzinę. Token hosta kończy pętlę przy shutdown.

Wyjątek nie powinien przypadkowo zatrzymać całego procesu; jednocześnie błędu nie wolno ukrywać. Obecna implementacja przechwytuje wyjątki utrzymania, ale nie rejestruje ich, co jest znanym ograniczeniem obserwowalności.

## Cykl życia

Host tworzy singletony przy pierwszym użyciu i zwalnia je podczas zamknięcia. Ma to znaczenie dla <code>IAsyncDisposable</code> publishera: kontrolowane zatrzymanie kończy kanał i czeka na worker. Nagłe przerwanie procesu nadal może utracić elementy, które nie dotarły do commit.
