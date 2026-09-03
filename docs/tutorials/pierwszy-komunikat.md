# Wyślij i odbierz pierwszy komunikat

W tym tutorialu uruchomisz broker, utworzysz topologię i prześlesz komunikat przez SDK .NET. Po zakończeniu zobaczysz identyfikator trwałej publikacji oraz treść odebraną przez konsumenta.

## Wymagania

Potrzebujesz .NET 10 SDK oraz dwóch terminali otwartych w katalogu głównym repozytorium.

## 1. Uruchom broker

W pierwszym terminalu przywróć pakiety, zbuduj rozwiązanie i uruchom runner z bazą w katalogu roboczym:

~~~powershell
dotnet restore
dotnet build --no-restore
$databasePath = Join-Path (Get-Location) ".data\rocketmq.db"
dotnet run --project src/Runner/RocketMQ.Runner --no-build -- --RocketMQ:Persistence:DatabasePath=$databasePath
~~~

Runner tworzy katalog bazy, konfiguruje SQLite w trybie WAL i uruchamia gRPC pod adresem <code>http://localhost:50051</code>.

## 2. Uruchom klienta

W drugim terminalu uruchom projekt przykładowy:

~~~powershell
dotnet run --project examples/RocketMQ.Example
~~~

Program wykonuje kolejno następujące operacje:

1. Rejestruje klientów gRPC przez <code>AddRocketMQClient</code>.
2. Deklaruje wymianę typu topic i trwałą kolejkę.
3. Tworzy wiązanie dla klucza <code>orders.*</code>.
4. Uruchamia pętlę konsumenta.
5. Publikuje treść <code>order-123</code>.
6. Zwraca <code>ConsumeResult.Success</code>, co powoduje wysłanie <code>Ack</code>.

Oczekiwany wynik ma tę postać:

~~~text
Publikacja: Accepted, kolejki: 1
Odebrano: order-123
Komunikat został potwierdzony.
~~~

Identyfikatory wymiany i kolejki zawierają losowy sufiks, więc kolejne uruchomienie nie korzysta ze starej topologii.

## 3. Zatrzymaj broker

W pierwszym terminalu naciśnij <code>Ctrl+C</code>. Generic Host zatrzyma serwer gRPC i zwolni singletony, w tym asynchroniczny publisher SQLite.

## Następne kroki

- [Opublikuj komunikat idempotentnie](../how-to/publikuj-idempotentnie.md), aby bezpiecznie powtarzać żądanie po utracie odpowiedzi.
- [Poznaj semantykę dostarczania](../explanation/semantyka-dostarczania.md), aby zrozumieć dzierżawy, redelivery i dead letter.
- [Sprawdź SDK .NET](../reference/dotnet-sdk.md), aby poznać pełne sygnatury klientów.
