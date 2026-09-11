# gRPC i Protocol Buffers

gRPC definiuje zdalne operacje w pliku <code>.proto</code>, a narzędzia generują typowane klasy klienta i serwera. W RocketMQ kontrakt obejmuje usługi Producer, Consumer i Admin.

## Dlaczego kontrakt jest osobnym artefaktem

Protocol Buffers opisuje strukturę wiadomości niezależnie od implementacji C#. Numery pól są częścią formatu binarnego i nie powinny być ponownie używane po usunięciu pola. Zasady kompatybilnej ewolucji opisuje [przewodnik proto3](https://protobuf.dev/programming-guides/proto3/).

Wszystkie bieżące RPC są unary: jedno żądanie i jedna odpowiedź. Konsument nie korzysta ze streamingu; SDK implementuje pętlę odpytywania <code>LeaseNext</code>.

## Warstwy wywołania

~~~mermaid
flowchart LR
    APP[Aplikacja] --> SDK[SDK .NET]
    SDK --> STUB[Wygenerowany klient]
    STUB -->|HTTP/2 + TLS lub mTLS| SERVICE[Usługa gRPC]
    SERVICE --> CORE[Port Core]
~~~

gRPC używa HTTP/2 jako transportu, a protobuf jako domyślnego formatu wiadomości. Podstawowe pojęcia opisują [wprowadzenie gRPC](https://grpc.io/docs/what-is-grpc/introduction/) i [core concepts](https://grpc.io/docs/what-is-grpc/core-concepts/).

## Statusy

Błędy walidacji i domenowe są mapowane na kody gRPC. Przykładowo brak exchange daje <code>NotFound</code>, konflikt PublishId daje <code>AlreadyExists</code>, a niepoprawny timeout <code>InvalidArgument</code>. Pełna tabela znajduje się w [referencji błędów](../reference/bledy.md).

## Bezpieczeństwo

Bieżący Runner domyślnie nasłuchuje na <code>https://localhost:50051</code> po HTTP/2 z TLS. Certyfikat serwera zabezpiecza poufność transmisji i pozwala klientowi zweryfikować serwer. Opcjonalne mTLS żąda dodatkowo certyfikatu klienta i podczas handshake buduje jego łańcuch do skonfigurowanego prywatnego CA. Walidacja nie jest powtarzana dla każdego RPC na tym samym połączeniu HTTP/2.

mTLS uwierzytelnia klienta. Opcjonalna autoryzacja mapuje SHA-256 certyfikatu na stabilny <code>ClientId</code> i niezależne role dla Producer, Consumer oraz Admin. Każda rola może być globalna albo ograniczona do dokładnych, case-sensitive nazw exchange/kolejek. Walidacja łańcucha nie jest powtarzana przy RPC; handler aplikacyjny oblicza fingerprint i tworzy claims, a serwis sprawdza zasób żądania. Lease jest związany ze stabilnym <code>ClientId</code>, więc inny klient nie może go zakończyć. Broker nadal nie ma limitów per klient ani trwałego audytu bezpieczeństwa. Jawny tryb HTTP jest dozwolony wyłącznie na loopback, gdy mTLS jest wyłączone.
