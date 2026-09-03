# Skonfiguruj SQLite i batching publikacji

Ta instrukcja zmienia lokalizację bazy oraz granice batcha trwałych publikacji.

## Ustaw konfigurację

Przekaż wartości przez argumenty polecenia:

~~~powershell
$databasePath = Join-Path (Get-Location) ".data\rocketmq.db"
dotnet run --project src/Runner/RocketMQ.Runner -- --RocketMQ:Persistence:DatabasePath=$databasePath --RocketMQ:Persistence:PublishBatchSize=64 --RocketMQ:Persistence:PublishBatchDelay=00:00:00.002
~~~

<code>PublishBatchSize</code> musi być dodatnią liczbą całkowitą. <code>PublishBatchDelay</code> musi być nieujemnym, skończonym przedziałem czasu.

## Dobierz granice batcha

Zwiększenie rozmiaru batcha może rozłożyć koszt jednego commitu SQLite na więcej publikacji. Zwiększenie opóźnienia daje workerowi więcej czasu na zebranie batcha, ale może podnieść opóźnienie pojedynczego żądania.

Nie traktuj wartości większej jako automatycznie lepszej. Porównaj ustawienia na świeżych bazach przy tym samym obciążeniu, korzystając z [procedury benchmarku](uruchom-benchmark.md).

## Sprawdź wartości domyślne

Jeżeli pominiesz ustawienia batcha, runner użyje:

- rozmiaru <code>32</code>;
- maksymalnego opóźnienia <code>1 ms</code>;
- wewnętrznej pojemności kanału <code>1024</code>, której obecnie nie można zmienić przez konfigurację.

Pełną tabelę zawiera [reference konfiguracji](../reference/konfiguracja.md).

## Następne kroki

- [Zrozum atomową publikację](../explanation/publikacja-i-sqlite.md).
- [Zrozum kanały i batching](../explanation/dotnet-asynchronicznosc.md).
