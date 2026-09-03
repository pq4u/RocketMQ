# Model brokera wiadomości

RocketMQ jest edukacyjnym brokerem wiadomości napisanym w C# na .NET 10. Nie jest powiązany z Apache RocketMQ. Producent publikuje komunikat do exchange, routing wybiera kolejki, a konsument dzierżawi komunikat z konkretnej kolejki.

~~~mermaid
flowchart LR
    P[Producent] -->|Publish| E[Exchange]
    E -->|binding + routing key| Q1[Kolejka A]
    E -->|binding + routing key| Q2[Kolejka B]
    Q1 -->|LeaseNext| C1[Konsument]
    C1 -->|Ack albo Nack| Q1
~~~

## Najważniejsze pojęcia

- **Exchange** nie przechowuje komunikatów. Określa algorytm routingu.
- **Binding** łączy exchange z kolejką i zawiera wzorzec klucza routingu.
- **Kolejka** przechowuje osobną kopię logiczną komunikatu dla danego odbiorcy.
- **Lease** to czasowa, wyłączna dzierżawa. Po jej wygaśnięciu komunikat może być dostarczony ponownie.
- **Ack** kończy obsługę i usuwa komunikat z kolejki.
- **Nack** zwraca komunikat do kolejki albo przenosi go do stanu dead-letter.

Projekt realizuje semantykę **at-least-once**: poprawnie potwierdzony komunikat nie powinien wrócić, ale awaria przed potwierdzeniem może wywołać ponowne dostarczenie. Kod konsumenta powinien być idempotentny.

## Co jest trwałe

Bieżący host zapisuje topologię, publikacje, kopie kolejkowe i stan dzierżaw w SQLite. Wewnętrzny <code>Channel&lt;T&gt;</code> służy wyłącznie do przyjęcia i grupowania żądań zapisu. Nie jest trwałą kolejką brokera: dane w samym kanale znikają wraz z procesem.

Szczegóły: [publikacja i SQLite](publikacja-i-sqlite.md), [semantyka dostarczania](semantyka-dostarczania.md) i [routing](routing.md).

## Świadome ograniczenia

Obecna implementacja jest prototypem uruchamianym jako pojedynczy proces. Nie ma uwierzytelniania, TLS, replikacji, klastra ani administracyjnego API do przeglądania dead letters. Adapter WAL jest szkieletem i nie nadaje się do uruchomienia.

