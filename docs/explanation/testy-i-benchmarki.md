# Testy i benchmarki

Testy są częścią specyfikacji zachowania projektu. Obejmują cztery różne poziomy.

| Zestaw | Co sprawdza |
|---|---|
| architecture tests | kierunek zależności między projektami |
| contract tests | wspólne zachowanie adapterów store |
| runner unit/integration | routing, leasing, SQLite i scenariusze trwałości |
| gRPC tests | publiczne mapowanie żądań, odpowiedzi i błędów |

## Testy kontraktowe

Fixture uruchamia te same przypadki dla implementacji portu. To chroni semantykę przed dostosowaniem testów do szczegółów adaptera. Nowy adapter powinien spełnić istniejący kontrakt, a nie osłabiać go.

## Testy integracyjne SQLite

Tworzą rzeczywistą bazę tymczasową i weryfikują migracje, transakcje, idempotencję, routing oraz ponowne otwarcie. Nie zastępują jednak testów awarii procesu w dokładnie wybranych punktach ani testów na wielu maszynach.

## Benchmark

Projekt benchmarkowy jest oddzielnym klientem gRPC. Mierzy obserwowane tempo i opóźnienia end-to-end dla wybranego scenariusza. Wynik zależy od sprzętu, systemu plików, ustawień batcha, współbieżności i liczby kolejek.

Nie należy traktować pojedynczej liczby jako gwarancji produktu. Raport powinien podać commit, konfigurację, rozgrzewkę, liczbę prób, percentyle i ograniczenia środowiska. Składnię programu opisuje [referencja benchmarku](../reference/benchmark-cli.md).

## Minimalna walidacja zmiany

~~~powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build --verbosity normal
./tools/verify-docs.ps1
~~~

