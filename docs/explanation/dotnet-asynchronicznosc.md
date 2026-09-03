# Asynchroniczność i współbieżność w .NET

Ten rozdział wyjaśnia mechanizmy użyte w projekcie, a nie pełny model współbieżności platformy.

## Task i async/await

<code>Task</code> reprezentuje operację, która zakończy się w przyszłości. <code>await</code> nie oznacza utworzenia nowego wątku: zwalnia bieżący przepływ do czasu ukończenia operacji i później kontynuuje metodę. W RocketMQ jest to istotne dla wywołań gRPC, SQLite i oczekiwania na miejsce w kanale.

Metody przyjmują <code>CancellationToken</code>. Token przekazuje żądanie anulowania, lecz sam nie przerywa kodu; każda warstwa musi go obserwować i przekazywać dalej.

## Channel

<code>System.Threading.Channels</code> udostępnia asynchroniczną kolejkę producent-konsument. Oficjalny opis i znaczenie opcji bounded channel zawiera [dokumentacja .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels).

W <code>SqliteMessagePublisher</code> kanał jest ograniczony do 1024 elementów:

- <code>SingleReader=true</code>, bo batch buduje jeden worker;
- <code>SingleWriter=false</code>, bo publikuje wiele wywołań gRPC;
- <code>FullMode=Wait</code>, więc pełny bufor wywołuje asynchroniczne oczekiwanie.

Kanał nie jest kolejką domenową ani magazynem trwałym. Jest buforem pracy między równoległymi żądaniami a jednym writerem SQLite.

## TaskCompletionSource

Każdy element kanału niesie <code>TaskCompletionSource&lt;PublishResult&gt;</code>. Worker kończy go dopiero po commit albo ustawia wyjątek po rollback. Dzięki temu żądanie gRPC może oczekiwać na wynik konkretnego elementu, mimo że zapisano go w batchu z innymi.

Opcja <code>RunContinuationsAsynchronously</code> zapobiega uruchamianiu całego kodu kontynuacji bezpośrednio na wątku workera kończącego zadanie.

## SemaphoreSlim

<code>SemaphoreSlim(1, 1)</code> jest asynchroniczną bramką do sekcji zapisu. W przeciwieństwie do <code>lock</code> pozwala oczekiwać przez <code>WaitAsync</code> bez blokowania wątku. Sekcja <code>finally</code> zawsze zwalnia semafor.

## Interlocked i Volatile

Te operacje synchronizują prosty stan między wątkami bez cięższej blokady. <code>Interlocked</code> wykonuje atomową zmianę, a <code>Volatile</code> zapewnia odczyt lub zapis z właściwą widocznością pamięci. Projekt używa ich do bezpiecznego sterowania zamykaniem publishera.

## IAsyncDisposable

Asynchroniczne zwalnianie pozwala zakończyć writer, opróżnić przyjętą pracę i zwolnić zasoby bez synchronicznego blokowania. Po rozpoczęciu shutdown nowe publikacje są odrzucane, kanał jest domykany, a host oczekuje na worker.

