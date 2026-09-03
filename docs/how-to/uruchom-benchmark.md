# Uruchom benchmark publikacji

Ta instrukcja mierzy zakończone operacje trwałej publikacji przez gRPC. Benchmark nie jest testem poprawności i nie konsumuje zapisanych komunikatów.

## Przygotuj świeżą bazę

Zatrzymaj poprzedni broker. Wybierz nową ścieżkę bazy dla każdego porównywalnego uruchomienia, a następnie uruchom runner:

~~~powershell
$databasePath = Join-Path (Get-Location) ".data\benchmark-01.db"
dotnet run --project src/Runner/RocketMQ.Runner -- --RocketMQ:Persistence:DatabasePath=$databasePath
~~~

Nie usuwaj ani nie kopiuj aktywnej bazy w trakcie pomiaru.

## Uruchom scenariusz bazowy

W drugim terminalu ustaw tę samą zmienną <code>$databasePath</code> i wykonaj:

~~~powershell
dotnet run --project tools/RocketMQ.Benchmark -- --endpoint http://localhost:50051 --database-path $databasePath
~~~

Domyślny scenariusz używa 32 workerów, payloadu 1 KiB, 30 sekund rozgrzewki i 15 minut pomiaru. Raport JSON trafia do <code>artifacts/benchmarks</code>.

## Włącz diagnostykę etapów

Jeżeli broker i benchmark pochodzą z tego samego buildu, dodaj:

~~~powershell
dotnet run --project tools/RocketMQ.Benchmark -- --endpoint http://localhost:50051 --database-path $databasePath --detailed-timings true
~~~

Diagnostyka zmienia ilość wykonywanej pracy. Porównuj ze sobą wyłącznie uruchomienia z tym samym ustawieniem.

## Porównaj wyniki

Wykonaj co najmniej trzy powtórzenia przy niezmienionym kodzie, runtime, sprzęcie, bazie początkowej i konfiguracji. Porównaj throughput, p50, p95, p99, maksimum, błędy oraz wzrost plików SQLite.

Nie łącz w jedną próbę raportów o różnej liczbie workerów. Pełne parametry opisuje [reference CLI benchmarku](../reference/benchmark-cli.md).

## Następne kroki

- [Zrozum metodologię pomiarów](../explanation/testy-i-benchmarki.md).
- [Sprawdź status adaptera WAL](../reference/status-funkcji.md#persistence).
