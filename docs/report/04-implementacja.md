# 4. Implementacja

## Publikowanie

ProducerService waliduje protobuf i buduje Envelope. SqliteMessagePublisher przyjmuje PendingPublish do kanału o pojemności 1024. Jeden worker buduje batch do skonfigurowanego rozmiaru albo czasu, otwiera transakcję i obsługuje elementy w kolejności.

Dla każdego PublishId obliczany jest fingerprint żądania. Istniejący zgodny wpis zwraca poprzedni wynik, a niezgodny kończy się konfliktem. Nowa publikacja odczytuje exchange i bindingi, wykonuje routing i zapisuje jeden wiersz messages na każdą unikalną kolejkę.

## Routing

Direct porównuje tekst dokładnie, fanout ignoruje klucz, a topic pracuje na segmentach oddzielonych kropką. Gwiazdka reprezentuje jeden segment, hash zero lub więcej. Wynik jest deduplikowany.

## Dostarczenie

LeaseNext wybiera najstarszy dostępny wiersz. Wiadomość dostępna to taka, która oczekuje albo ma wygasły lease. Aktualizacja ustawia nowy LeaseId, termin i zwiększa DeliveryCount. Ack usuwa wiersz, Nack czyści lease albo oznacza dead-letter.

Przed kolejną dzierżawą store porównuje DeliveryCount z limitem kolejki. Osiągnięcie dodatniego MaxDeliveryCount powoduje automatyczny dead-letter; publiczne Admin API ustawia limit 10.

## Trwałość

SqliteDatabase włącza WAL, synchronous FULL, klucze obce i pięciosekundowy busy timeout. Transakcje są jawne, a błędy prowadzą do rollback. Szczegółowy schemat dokumentuje [referencja SQLite](../reference/sqlite-schema.md).

## SDK

SDK ukrywa wygenerowane stuby. Producer potrafi ponowić ResourceExhausted. Consumer prowadzi pętlę unary polling i tłumaczy wynik handlera na Ack lub Nack. Admin udostępnia minimalne operacje tworzenia topologii.

## Utrzymanie

BackgroundService wykonuje purge po starcie i co godzinę. Usuwa stare rekordy idempotencji i dead letters. Wartości retencji są stałe w kodzie, a przechwycone błędy maintenance nie są obecnie logowane.
