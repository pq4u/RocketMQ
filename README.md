# RocketMQ

RocketMQ to edukacyjny broker wiadomości napisany w C# dla .NET 10. Obsługuje nazwane kolejki, routing przez wymiany, konkurujących konsumentów oraz jawne potwierdzanie przetworzenia przez gRPC.

> **Status:** aktywny prototyp jednowęzłowy. Runner zapisuje dane w SQLite. Nie udostępnia TLS, uwierzytelniania, klastrowania ani interfejsu administracyjnego HTTP. Projekt nie jest Apache RocketMQ i nie implementuje protokołu AMQP.

## Szybki start

Potrzebujesz zestawu .NET 10 SDK. W katalogu głównym repozytorium wykonaj:

~~~powershell
dotnet restore
dotnet build --no-restore
$databasePath = Join-Path (Get-Location) ".data\rocketmq.db"
dotnet run --project src/Runner/RocketMQ.Runner -- --RocketMQ:Persistence:DatabasePath=$databasePath
~~~

Broker nasłuchuje pod adresem <code>http://localhost:50051</code> przez HTTP/2 bez TLS. Zatrzymaj proces skrótem <code>Ctrl+C</code>.

W drugim terminalu uruchom kompletny przykład klienta:

~~~powershell
dotnet run --project examples/RocketMQ.Example
~~~

Przykład tworzy wymianę i kolejkę, publikuje komunikat, odbiera go oraz wysyła <code>Ack</code>.

## Co dalej

- [Pierwszy komunikat](docs/tutorials/pierwszy-komunikat.md) prowadzi przez cały przepływ krok po kroku.
- [Indeks dokumentacji](docs/index.md) rozdziela instrukcje, wyjaśnienia i reference.
- [Architektura systemu](docs/explanation/architektura.md) pokazuje zależności projektów i przepływ danych.
- [Status funkcji](docs/reference/status-funkcji.md) odróżnia elementy gotowe, eksperymentalne i planowane.
- [Raport techniczny](docs/report/index.md) przedstawia projekt w formie ciągłej narracji.
- [Plan i zasady pisania dokumentacji](docs/documentation-guide.md) opisuje strukturę, źródła prawdy i definicję ukończenia.

## Walidacja zmian

~~~powershell
dotnet test --verbosity normal
./tools/verify-docs.ps1
~~~

Zmiana zachowania brokera musi aktualizować odpowiadającą jej dokumentację i, gdy zmienia decyzję architektoniczną, właściwy ADR.
