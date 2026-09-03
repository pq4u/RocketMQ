# Konfiguracja Runnera

Konfigurację dostarcza standardowy Generic Host: pliki appsettings, zmienne środowiskowe i argumenty wiersza poleceń. Poniższe wartości opisują bieżący kod.

| Klucz | Wymagany | Domyślna wartość | Ograniczenia |
|---|---:|---|---|
| <code>RocketMQ:Persistence:DatabasePath</code> | tak | wpis w appsettings dla lokalnego repo | bezwzględna ścieżka lokalna z katalogiem; UNC odrzucone |
| <code>RocketMQ:Persistence:PublishBatchSize</code> | nie | <code>32</code> | liczba całkowita większa od zera |
| <code>RocketMQ:Persistence:PublishBatchDelay</code> | nie | <code>00:00:00.001</code> | nieujemny TimeSpan w kulturze invariant |

Port gRPC jest stały: <code>50051</code>, <code>ListenAnyIP</code>, HTTP/2 bez TLS.

Przykład:

~~~powershell
dotnet run --project src/Runner/RocketMQ.Runner -- --RocketMQ:Persistence:DatabasePath=D:\RocketMQData\rocketmq.db --RocketMQ:Persistence:PublishBatchSize=64 --RocketMQ:Persistence:PublishBatchDelay=00:00:00.002
~~~

Kanał publishera ma stałą pojemność 1024. Retencja PublishId wynosi 24 godziny, retencja dead letters 30 dni, a maintenance działa co godzinę. Te wartości nie są obecnie konfigurowalne.

