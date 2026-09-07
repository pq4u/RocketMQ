# Status funkcji

Stan zweryfikowany względem bieżącego drzewa roboczego 7 września 2026.

| Obszar | Status | Uwagi |
|---|---|---|
| direct, fanout, topic routing | działa i jest testowane | brak default exchange |
| topologia w SQLite | działa i jest testowana | brak listowania przez publiczne gRPC Admin |
| trwała publikacja | działa i jest testowana | transakcyjny batch |
| PublishId | działa i jest testowane | okno 24 godziny |
| lease, Ack, Nack, redelivery | działa i jest testowane | unary polling |
| dead-letter zapis i port Core | działa | brak publicznego API browse |
| SDK .NET | działa | domyślnie https://localhost:50051 |
| benchmark gRPC | działa | direct i fanout |
| SQLite WAL mode | działa | jeden writer, lokalny plik |
| własny adapter WAL | niezaimplementowany | metody zgłaszają NotImplementedException |
| TLS | działa i jest testowany | domyślny endpoint HTTPS; HTTP tylko na loopback |
| mTLS | działa i jest testowane, opt-in | prywatne CA z PEM/DER; walidacja przy handshake; restart przy rotacji |
| autoryzacja | działa i jest testowana, opt-in | fingerprint SHA-256 → ClientId; niezależne Publish, Consume i Admin; brak ACL zasobowych |
| HA, replikacja, klaster | brak | pojedynczy proces |
| streaming konsumenta | brak | SDK odpytuje LeaseNext |
| automatyczne odnowienie lease | brak | dobierz visibility timeout |
| automatyczny MaxDeliveryCount | działa i jest testowany | Admin deklaruje kolejki z limitem 10; 0 oznacza bez limitu |
| telemetryka produkcyjna | częściowa | diagnostyka Publish jest opt-in |

„Działa” oznacza zachowanie obecne w kodzie i pokryte odpowiednimi testami, nie deklarację gotowości produkcyjnej. Otwarte decyzje znajdują się w [docs/decisions](../decisions/).
