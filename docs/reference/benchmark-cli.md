# Referencja CLI benchmarku

Wymagane są działający broker oraz istniejąca lokalna baza SQLite utworzona przez ten broker.

~~~powershell
dotnet run --project tools/RocketMQ.Benchmark -- --endpoint https://localhost:50051 --database-path D:\RocketMQData\rocketmq.db
~~~

| Opcja | Domyślnie | Reguła |
|---|---|---|
| <code>--endpoint</code> | wymagane | bezwzględny URI HTTP(S) |
| <code>--database-path</code> | wymagane | bezwzględna ścieżka lokalna do istniejącej bazy |
| <code>--duration</code> | 00:15:00 | większe od zera |
| <code>--warmup</code> | 00:00:30 | nieujemne |
| <code>--workers</code> | 32 | dodatnia liczba |
| <code>--payload-bytes</code> | 1024 | 1–16777216 |
| <code>--routing</code> | direct | direct albo fanout |
| <code>--queue-count</code> | 1 | dodatnia; dla direct dokładnie 1 |
| <code>--detailed-timings</code> | false | true albo false |
| <code>--results-dir</code> | artifacts/benchmarks | katalog raportów JSON |
| <code>--client-certificate-path</code> | brak | bezwzględna ścieżka PFX/P12 albo PEM; wymaga endpointu HTTPS |
| <code>--client-certificate-key-path</code> | brak | klucz prywatny dla certyfikatu PEM |
| <code>--client-certificate-password-env</code> | brak | nazwa zmiennej środowiskowej zawierającej hasło PFX lub zaszyfrowanego klucza PEM |

Narzędzie tworzy unikalną topologię, nie konsumuje wiadomości i nie ponawia błędnego RPC. Raport zawiera liczniki, throughput, p50/p95/p99/max, błędy, środowisko, informację o użyciu mTLS oraz rozmiary plików db, WAL i SHM. Detailed timings wymagają zgodnych buildów klienta i serwera.

Dla HTTPS certyfikat brokera musi być zaufany przez system uruchamiający benchmark.

Benchmark ładuje certyfikat klienta raz i używa jednego kanału HTTP/2. Przy włączonej autoryzacji certyfikat musi mieć role <code>Admin</code> i <code>Publish</code>, ponieważ narzędzie tworzy topologię przed pomiarem publikacji. Przykład pełnego polecenia znajduje się w instrukcji [Skonfiguruj wzajemne TLS](../how-to/skonfiguruj-mtls.md).
