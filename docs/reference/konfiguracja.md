# Konfiguracja Runnera

Konfigurację dostarcza standardowy Generic Host: pliki appsettings, zmienne środowiskowe i argumenty wiersza poleceń. Poniższe wartości opisują bieżący kod.

| Klucz | Wymagany | Domyślna wartość | Ograniczenia |
|---|---:|---|---|
| <code>RocketMQ:Persistence:DatabasePath</code> | tak | wpis w appsettings dla lokalnego repo | bezwzględna ścieżka lokalna z katalogiem; UNC odrzucone |
| <code>RocketMQ:Persistence:PublishBatchSize</code> | nie | <code>32</code> | liczba całkowita większa od zera |
| <code>RocketMQ:Persistence:PublishBatchDelay</code> | nie | <code>00:00:00.001</code> | nieujemny TimeSpan w kulturze invariant |
| <code>RocketMQ:Management:Enabled</code> | nie | <code>false</code> | <code>true</code> uruchamia panel i REST API |
| <code>RocketMQ:Management:Url</code> | nie | <code>https://localhost:50052</code> | bezwzględny URI HTTP(S), wyłącznie loopback i port inny niż gRPC |
| <code>Kestrel:Endpoints:Grpc:Url</code> | nie | <code>https://localhost:50051</code> | bezwzględny URI HTTPS; HTTP tylko dla loopback |
| <code>Kestrel:Endpoints:Grpc:Protocols</code> | nie | <code>Http2</code> | wyłącznie <code>Http2</code> |
| <code>Kestrel:Endpoints:Grpc:Certificate:Path</code> | produkcja | brak | PFX albo PEM/CRT; lokalnie może zostać użyty certyfikat deweloperski |
| <code>Kestrel:Endpoints:Grpc:Certificate:KeyPath</code> | dla PEM/CRT | brak | ścieżka do odpowiadającego klucza prywatnego |
| <code>Kestrel:Endpoints:Grpc:Certificate:Password</code> | zależnie od certyfikatu | brak | przekazuj przez źródło sekretów, nie zapisuj w repozytorium |
| <code>RocketMQ:Security:MutualTls:Enabled</code> | nie | <code>false</code> | po włączeniu wszystkie endpointy muszą być HTTPS i wymagają certyfikatu klienta |
| <code>RocketMQ:Security:MutualTls:TrustedClientCaPath</code> | gdy mTLS włączone | brak | bezwzględna ścieżka do CA klientów w PEM lub DER; PEM może zawierać intermediates |
| <code>RocketMQ:Security:MutualTls:RevocationMode</code> | nie | <code>NoCheck</code> | <code>NoCheck</code>, <code>Offline</code> albo <code>Online</code> |
| <code>RocketMQ:Security:Authorization:Enabled</code> | nie | <code>false</code> | wymaga włączonego mTLS |
| <code>RocketMQ:Security:Authorization:Clients:{ClientId}:CertificateSha256Fingerprints</code> | gdy autoryzacja włączona | brak | co najmniej jeden unikalny SHA-256; dozwolone są dwukropki |
| <code>RocketMQ:Security:Authorization:Clients:{ClientId}:Permissions</code> | nie | pusta lista | globalne <code>Publish</code>, <code>Consume</code>, <code>Admin</code>; zachowują dostęp do wszystkich odpowiednich zasobów |
| <code>...:Resources:Exchanges:Publish</code> | nie | pusta lista | dokładne, case-sensitive nazwy exchange, do których klient może publikować |
| <code>...:Resources:Exchanges:Admin</code> | nie | pusta lista | dokładne, case-sensitive nazwy exchange do operacji administracyjnych |
| <code>...:Resources:Queues:Consume</code> | nie | pusta lista | dokładne, case-sensitive nazwy kolejek do Lease/Ack/Nack |
| <code>...:Resources:Queues:Admin</code> | nie | pusta lista | dokładne, case-sensitive nazwy kolejek do operacji administracyjnych |

Runner używa standardowego schematu konfiguracji Kestrela. Lokalny wariant korzysta z certyfikatu utworzonego przez <code>dotnet dev-certs https --trust</code>. Kestrel obsługuje również certyfikat domyślny oraz certyfikat wskazany przez magazyn systemowy.

Przykład:

~~~powershell
dotnet run --project src/Runner/RocketMQ.Runner -- --RocketMQ:Persistence:DatabasePath=D:\RocketMQData\rocketmq.db --RocketMQ:Persistence:PublishBatchSize=64 --RocketMQ:Persistence:PublishBatchDelay=00:00:00.002
~~~

Przykład z panelem administracyjnym:

~~~powershell
dotnet run --project src/Runner/RocketMQ.Runner -- --RocketMQ:Persistence:DatabasePath=D:\RocketMQData\rocketmq.db --RocketMQ:Management:Enabled=true
~~~

Panel pozostaje wyłączony domyślnie. W pierwszej wersji nie ma osobnego uwierzytelniania HTTP, dlatego serwer odrzuca adresy inne niż loopback. REST i gRPC działają w oddzielnych wewnętrznych hostach Kestrela oraz muszą używać różnych portów.

Przykład wystawienia endpointu HTTPS z certyfikatem PFX:

~~~powershell
$env:Kestrel__Endpoints__Grpc__Url = "https://0.0.0.0:50051"
$env:Kestrel__Endpoints__Grpc__Certificate__Path = "D:\certs\rocketmq.pfx"
$env:Kestrel__Endpoints__Grpc__Certificate__Password = "<sekret>"
dotnet run --project src/Runner/RocketMQ.Runner -- --RocketMQ:Persistence:DatabasePath=D:\RocketMQData\rocketmq.db
~~~

Nie istnieje automatyczny fallback z HTTPS na HTTP. Brakujący albo niepoprawny certyfikat zatrzymuje start. Jawny lokalny tryb nieszyfrowany wymaga ustawienia URL na <code>http://localhost:50051</code>; konfiguracja HTTP dla <code>AnyIP</code> jest odrzucana.

Po włączeniu mTLS tryb HTTP jest odrzucany również na loopback, a jawne ustawienie <code>ClientCertificateMode</code> inne niż <code>RequireCertificate</code> jest błędem. Broker ufa certyfikatom klienta wyłącznie z łańcucha prowadzącego do <code>TrustedClientCaPath</code>; systemowy magazyn zaufania nie rozszerza tej listy. Zmiana pliku CA wymaga restartu. Kompletny przykład znajduje się w instrukcji [Skonfiguruj wzajemne TLS](../how-to/skonfiguruj-mtls.md).

Gdy autoryzacja jest włączona, fingerprint może należeć tylko do jednego <code>ClientId</code>, a uprawnienia są niezależne. Klient musi mieć co najmniej jedno uprawnienie globalne lub zasobowe. Nieznany certyfikat zwraca <code>Unauthenticated</code>; brak uprawnienia do dokładnej nazwy zasobu zwraca <code>PermissionDenied</code>. <code>Bind</code> wymaga Admin dla wskazanego exchange i kolejki. Konfiguracja jest ładowana przy starcie, odrzuca nieznane sekcje, duplikaty oraz nazwy z otaczającymi spacjami i wymaga restartu po zmianie.

Kanał publishera ma stałą pojemność 1024. Retencja PublishId wynosi 24 godziny, retencja dead letters 30 dni, a maintenance działa co godzinę. Te wartości nie są obecnie konfigurowalne.
