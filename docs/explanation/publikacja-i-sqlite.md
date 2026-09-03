# Publikacja, batching i SQLite

Najważniejszą własnością publikacji jest kolejność: odpowiedź <code>Accepted</code> może zostać zwrócona dopiero po trwałym zatwierdzeniu danych.

~~~mermaid
sequenceDiagram
    participant G as gRPC
    participant P as SqliteMessagePublisher
    participant C as Channel
    participant W as Worker
    participant D as SQLite
    G->>P: PublishAsync
    P->>C: WriteAsync(PendingPublish)
    W->>C: odczyt batcha
    W->>D: BEGIN + zapis
    D-->>W: COMMIT
    W-->>P: TaskCompletionSource.SetResult
    P-->>G: PublishResult
~~~

## Dlaczego batch

Koszt transakcji i synchronizacji pliku można rozłożyć na kilka publikacji. Worker zbiera do <code>BatchSize</code> elementów, czekając najwyżej <code>BatchDelay</code>. Ustawienia te wpływają na kompromis między opóźnieniem a przepustowością.

Kanał ma pojemność 1024 i tryb <code>Wait</code>. Gdy producent wyprzedza dysk, <code>WriteAsync</code> asynchronicznie czeka na miejsce. To lokalny mechanizm backpressure; bieżący serwer nie mapuje tego czekania na gRPC <code>ResourceExhausted</code>.

## Atomowość

Publikacja i wszystkie kopie przeznaczone do kolejek są zapisywane w jednej transakcji. Albo widoczne są wszystkie, albo żadna. Dostęp do zapisu serializuje <code>SemaphoreSlim</code>, ponieważ SQLite dopuszcza jednego writera naraz.

Baza pracuje w trybie WAL, z <code>synchronous=FULL</code>, kluczami obcymi i <code>busy_timeout=5000</code>. WAL pozwala czytelnikom działać równolegle z writerem, lecz nadal istnieje tylko jeden writer. Szczegóły mechanizmu opisuje [dokumentacja SQLite WAL](https://sqlite.org/wal.html), a znaczenie poziomu synchronizacji [PRAGMA synchronous](https://sqlite.org/pragma.html#pragma_synchronous).

## Idempotencja

<code>PublishId</code> jest przechowywany przez 24 godziny. Powtórzenie identycznego żądania zwraca wcześniejszy wynik bez ponownego enqueue. Użycie tego samego identyfikatora z innymi danymi jest konfliktem.

## Retencja

Hosted service raz na godzinę usuwa publikacje idempotencyjne starsze niż 24 godziny i dead letters starsze niż 30 dni. Okres dead-letter jest obecnie stałą kodu, nie opcją konfiguracji.

