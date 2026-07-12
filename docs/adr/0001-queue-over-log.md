# ADR-0001: Przejście z modelu log (Kafka-style) na model kolejki (RabbitMQ-style)

- **Status:** Accepted
- **Date:** 2026-07-12
- **Deciders:** Team

## Context

System został zaprojektowany z `IPersistenceStore` jako append-only log
(offset/sequence-number based, styl Kafki) z metodami `AppendAsync` i
`ReadFromAsync(afterSequenceNumber)`. Ten model zakłada, że konsument
samodzielnie śledzi swój offset i robi replay — nie ma koncepcji "odebrania"
i "potwierdzenia" wiadomości.

Docelowo system ma działać jak broker kolejkowy w stylu RabbitMQ:
- Konsument pobiera wiadomość (lease) i ma ograniczony czas na jej
  przetworzenie (visibility timeout).
- Po przetworzeniu potwierdza (ack) — wiadomość jest trwale usunięta.
- Jeśli odrzuca (nack) — wiadomość wraca do puli (requeue=true) lub
  trafia do dead-letter (requeue=false).
- Jeśli konsument padnie bez ack/nack — wiadomość automatycznie wraca
  do puli po upływie visibility timeout.

## Decision

### Nowy port: `IMessageQueueStore`

Wprowadzamy nowy port `IMessageQueueStore` w `Core/Abstractions` z
metodami:
- `EnqueueAsync` — trwałe dodanie wiadomości do kolejki
- `LeaseNextAsync` — atomiczne pobranie najstarszej dostępnej wiadomości
  z visibility timeout
- `AckAsync` — trwałe usunięcie wiadomości
- `NackAsync` — zwrot do puli (requeue=true) lub dead-letter (requeue=false)
- `BrowseDeadLettersAsync` — diagnostyczny odczyt dead-letterów

### `IPersistenceStore` pozostaje

`IPersistenceStore` nie jest usuwany ani zastępowany. To fundamentalnie
inna abstrakcja (append-only log vs. destrukcyjny consume) i może być
potrzebny w przyszłości do:
- Event sourcing
- Audit trail
- Replay / odtwarzanie stanu

Koszt utrzymania jest bliski zeru (2 metody, ~40 linii).

### `InboundMessage` bez zmian

Stan kolejki (available/leased/acked/dead-lettered) żyje wyłącznie w
implementacji store'a. `InboundMessage` pozostaje czystym, immutable
rekordem danych. Nowe typy `LeasedMessage` i `DeadLetteredMessage`
opakowują `InboundMessage` kompozycją.

## Consequences

### Pozytywne
- System zyskuje semantykę competing consumers z at-least-once delivery.
- Visibility timeout zapewnia automatyczny redelivery przy awarii
  konsumenta — bez potrzeby zewnętrznego supervisora.
- Dead-letter daje obserwowalność poison messages.
- Zachowanie `IPersistenceStore` nie zamyka drogi do event sourcing.

### Negatywne
- Dwa porty persistence zamiast jednego — niewielkie, ale realne
  obciążenie kognitywne.
- SQLite adapter dla `IMessageQueueStore` wymaga atomowego
  UPDATE+RETURNING z pessimistic locking (BEGIN IMMEDIATE) — bardziej
  złożone niż prosty INSERT/SELECT z `IPersistenceStore`.

### Ryzyka
- Visibility timeout oparty na porównaniu timestampów wymaga, by zegar
  systemowy nie cofał się znacząco (NTP leap, VM suspend/resume). Na
  tym etapie akceptujemy to ryzyko.
- SQLite's single-writer model może stać się bottleneckiem przy dużej
  liczbie konkurencyjnych `LeaseNextAsync`. Mitygacja: eventual WAL
  adapter (`WalMessageQueueStore`).
