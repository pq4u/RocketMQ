# Management REST API i panel

Panel administracyjny jest opcjonalnym, lokalnym interfejsem do obsługi topologii, kolejek i dead letters. Po włączeniu Runner serwuje aplikację Blazor WebAssembly oraz JSON REST API z tego samego adresu. Domyślny URL to <code>https://localhost:50052</code>, a dokument OpenAPI jest dostępny pod <code>/openapi/v1.json</code>.

## Uruchomienie i granica bezpieczeństwa

~~~powershell
$databasePath = Join-Path (Get-Location) ".data\rocketmq.db"
dotnet run --project src/Runner/RocketMQ.Runner -- --RocketMQ:Persistence:DatabasePath=$databasePath --RocketMQ:Management:Enabled=true
~~~

Otwórz <code>https://localhost:50052</code>. Panel jest wyłączony domyślnie i nie ma własnego logowania. Bieżąca implementacja akceptuje wyłącznie adres loopback oraz wymaga portu innego niż endpoint gRPC. Wystawienie panelu przez reverse proxy lub na zdalnym interfejsie nie należy do wspieranego modelu bezpieczeństwa tej wersji.

## Funkcje panelu

- dashboard ze stanem topologii, licznikami ready/in-flight/dead-letter, uptime i metrykami minutowymi;
- tworzenie, listowanie i usuwanie exchange oraz kolejek;
- tworzenie, listowanie i usuwanie bindingów;
- statystyki kolejki i niedestrukcyjne przeglądanie wiadomości ready;
- podgląd payloadu, ograniczony do pierwszych 64 KiB jako tekst UTF-8 lub Base64;
- purge wiadomości ready, z zachowaniem aktywnych lease'ów i dead letters;
- przeglądanie, requeue, usuwanie pojedynczych wpisów i czyszczenie DLQ;
- testowa publikacja tekstu UTF-8 lub Base64, maksymalnie 1 MiB;
- bezpieczny podgląd wybranej konfiguracji i ACL; fingerprinty certyfikatów są maskowane.

Operacje usuwania w UI wymagają potwierdzenia. Usunięcie kolejki kasuje także jej wiadomości i bindingi zgodnie z relacjami SQLite. Requeue dead letter zeruje licznik dostarczeń oraz usuwa pola lease i przyczynę dead-letter.

## Endpointy

| Metoda i ścieżka | Znaczenie |
|---|---|
| <code>GET /health/live</code> | liveness procesu management |
| <code>GET /health/ready</code> | sprawdzenie dostępu do SQLite |
| <code>GET /api/v1/overview</code> | wersja, uptime i zagregowany stan brokera |
| <code>GET /api/v1/metrics?range=1h</code> | minutowe bucket'y dla <code>15m</code>, <code>1h</code>, <code>6h</code> lub <code>24h</code> |
| <code>GET /api/v1/exchanges</code> | lista exchange |
| <code>GET /api/v1/exchanges/{name}</code> | szczegóły exchange |
| <code>PUT /api/v1/exchanges/{name}</code> | deklaracja exchange; body <code>{"type":"Direct"}</code> |
| <code>DELETE /api/v1/exchanges/{name}</code> | usunięcie exchange i jego bindingów |
| <code>GET /api/v1/queues</code> | lista kolejek |
| <code>GET /api/v1/queues/{name}</code> | szczegóły kolejki |
| <code>PUT /api/v1/queues/{name}</code> | deklaracja kolejki; body <code>{"maxDeliveryCount":10}</code> |
| <code>DELETE /api/v1/queues/{name}</code> | usunięcie kolejki, wiadomości i bindingów |
| <code>GET /api/v1/queues/{name}/stats</code> | liczniki ready, in-flight i dead-letter |
| <code>GET /api/v1/exchanges/{name}/bindings</code> | bindingi danego exchange |
| <code>POST /api/v1/bindings</code> | utworzenie bindingu |
| <code>DELETE /api/v1/exchanges/{exchange}/bindings/{queue}?routingKey=...</code> | usunięcie bindingu |
| <code>GET /api/v1/queues/{name}/messages</code> | stronicowany browse ready |
| <code>GET /api/v1/queues/{name}/messages/{messageId}</code> | metadane i ograniczony preview payloadu |
| <code>POST /api/v1/queues/{name}/purge</code> | usunięcie available i wygasłych lease'ów |
| <code>GET /api/v1/queues/{name}/dead-letters</code> | stronicowany browse DLQ |
| <code>GET /api/v1/queues/{name}/dead-letters/{messageId}</code> | szczegóły dead letter |
| <code>POST /api/v1/queues/{name}/dead-letters/{messageId}/requeue</code> | powrót wiadomości do ready |
| <code>DELETE /api/v1/queues/{name}/dead-letters/{messageId}</code> | trwałe usunięcie dead letter |
| <code>DELETE /api/v1/queues/{name}/dead-letters</code> | wyczyszczenie DLQ kolejki |
| <code>POST /api/v1/publish</code> | testowa publikacja wiadomości |
| <code>GET /api/v1/configuration</code> | bezpieczny podgląd konfiguracji |
| <code>GET /api/v1/security/clients</code> | stan ACL bez pełnych fingerprintów |

Parametry <code>cursor</code> i <code>limit</code> dotyczą obu list wiadomości. Cursor jest nieprzezroczysty i należy przekazać go bez modyfikacji; domyślna strona ma 50, a maksymalna 100 pozycji. Browse używa punktu czasowego i górnego identyfikatora w cursorze, aby nowe publikacje nie przesuwały elementów między stronami.

## Błędy

Błędy mają media type <code>application/problem+json</code>. Obiekt zawiera standardowe pola Problem Details oraz rozszerzenia <code>code</code> i <code>traceId</code>. Stabilne kody to <code>validation_error</code> (400), <code>not_found</code> (404), <code>conflict</code> (409), <code>payload_too_large</code> (413) i <code>internal_error</code> (500).

Metryki panelu są diagnostyką procesu: historia obejmuje maksymalnie 24 godziny i znika przy restarcie. Nie zastępują Prometheus/OpenTelemetry ani trwałego audytu operacji administracyjnych.
