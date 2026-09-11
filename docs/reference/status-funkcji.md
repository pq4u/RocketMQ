# Status funkcji

Stan zweryfikowany względem bieżącego drzewa roboczego 11 września 2026.

| Obszar | Status | Uwagi |
|---|---|---|
| direct, fanout, topic routing | działa i jest testowane | brak default exchange |
| topologia w SQLite | działa i jest testowana | listowanie i CRUD przez lokalny REST; gRPC Admin zachowuje dotychczasowy kontrakt |
| trwała publikacja | działa i jest testowana | transakcyjny batch |
| PublishId | działa i jest testowane | okno 24 godziny |
| lease, Ack, Nack, redelivery | działa i jest testowane | unary polling |
| dead-letter zapis i port Core | działa i jest testowany | browse, podgląd, requeue, delete i clear przez lokalny REST |
| SDK .NET | działa | domyślnie https://localhost:50051 |
| benchmark gRPC | działa | direct i fanout |
| SQLite WAL mode | działa | jeden writer, lokalny plik |
| własny adapter WAL | niezaimplementowany | metody zgłaszają NotImplementedException |
| TLS | działa i jest testowany | domyślny endpoint HTTPS; HTTP tylko na loopback |
| mTLS | działa i jest testowane, opt-in | prywatne CA z PEM/DER; walidacja przy handshake; restart przy rotacji |
| autoryzacja | działa i jest testowana, opt-in | fingerprint SHA-256 → ClientId; globalne lub exact-name ACL dla exchange/kolejek; lease związany z ClientId |
| panel administracyjny i REST API | działa i jest testowany, opt-in | Blazor WebAssembly + MudBlazor; wyłącznie loopback; OpenAPI v1 |
| HA, replikacja, klaster | brak | pojedynczy proces |
| streaming konsumenta | brak | SDK odpytuje LeaseNext |
| automatyczne odnowienie lease | brak | dobierz visibility timeout |
| automatyczny MaxDeliveryCount | działa i jest testowany | Admin deklaruje kolejki z limitem 10; 0 oznacza bez limitu |
| telemetryka produkcyjna | częściowa | diagnostyka Publish jest opt-in; panel przechowuje metryki minutowe tylko w pamięci procesu przez maksymalnie 24 h |

„Działa” oznacza zachowanie obecne w kodzie i pokryte odpowiednimi testami, nie deklarację gotowości produkcyjnej. Otwarte decyzje znajdują się w [docs/decisions](../decisions/).
