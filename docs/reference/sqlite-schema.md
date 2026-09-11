# Schemat SQLite

Schemat jest tworzony automatycznie podczas pierwszego użycia. Tabela <code>schema_migrations</code> rejestruje zastosowane wersje; bieżący kod zna migracje 1, 2, 3 i 4. Start na bazie oznaczonej nowszą, nieznaną wersją jest odrzucany.

| Tabela | Rola |
|---|---|
| <code>exchanges</code> | nazwa, typ i trwałość exchange |
| <code>queues</code> | nazwa, trwałość i zapisane max_delivery_count |
| <code>bindings</code> | relacja exchange–queue–routing key |
| <code>messages</code> | kopie kolejkowe, stan lease, opcjonalny lease_owner_id, licznik i dead-letter |
| <code>publications</code> | PublishId, fingerprint, wynik i czas dla idempotencji |
| <code>publication_destinations</code> | kolejki wyniku publikacji |
| <code>persistence_log</code> | starszy port append-only IPersistenceStore |
| <code>schema_migrations</code> | wersje schematu |

## Kluczowe relacje i indeksy

Bindingi i wiadomości mają klucze obce z <code>ON DELETE CASCADE</code>. LeaseId jest unikalny. Indeksy wspierają wybór wiadomości po kolejce, stanie i kolejności enqueue, wyszukiwanie lease, przegląd dead letters, bindingi exchange i czyszczenie publikacji po czasie.

Migracja 3 dodaje nullable <code>messages.lease_owner_id</code>. Nowe dzierżawy powstałe przy włączonej autoryzacji zapisują stabilny <code>ClientId</code>; Ack/Nack sprawdzają go atomowo wraz z lease'em i opcjonalną nazwą kolejki. Starsze aktywne lease'y pozostają z wartością NULL i zachowują dotychczasowe zachowanie dla ścieżki bez właściciela.

Migracja 4 dodaje indeksy <code>ix_messages_queue_state_expiry</code> oraz <code>ix_messages_queue_message_id</code>. Wspierają one statystyki i stabilne stronicowanie panelu, rozróżnianie aktywnych i wygasłych lease'ów oraz ograniczony do kolejki podgląd pojedynczej wiadomości.

## Reprezentacja

Identyfikatory Guid są przechowywane jako BLOB. Daty UTC zapisuje się jako tekst w formacie round-trip. Stan wiadomości jest tekstem. Kod, a nie użytkownik, jest właścicielem schematu; ręczne modyfikacje bazy nie są wspieranym API.

## Ustawienia połączenia

Każde otwarte połączenie ustawia <code>journal_mode=WAL</code>, <code>synchronous=FULL</code>, <code>foreign_keys=ON</code> i <code>busy_timeout=5000</code>. Pliki <code>-wal</code> i <code>-shm</code> są częścią działającej bazy i należy je uwzględniać przy operacjach administracyjnych.
