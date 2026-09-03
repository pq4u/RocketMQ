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
    STUB -->|HTTP/2| SERVICE[Usługa gRPC]
    SERVICE --> CORE[Port Core]
~~~

gRPC używa HTTP/2 jako transportu, a protobuf jako domyślnego formatu wiadomości. Podstawowe pojęcia opisują [wprowadzenie gRPC](https://grpc.io/docs/what-is-grpc/introduction/) i [core concepts](https://grpc.io/docs/what-is-grpc/core-concepts/).

## Statusy

Błędy walidacji i domenowe są mapowane na kody gRPC. Przykładowo brak exchange daje <code>NotFound</code>, konflikt PublishId daje <code>AlreadyExists</code>, a niepoprawny timeout <code>InvalidArgument</code>. Pełna tabela znajduje się w [referencji błędów](../reference/bledy.md).

## Bezpieczeństwo

Bieżący Runner nasłuchuje na <code>http://localhost:50051</code> po HTTP/2 bez TLS i uwierzytelniania. Jest to konfiguracja lokalna. Wystawienie portu poza zaufane środowisko wymaga zaprojektowania TLS, tożsamości klienta, autoryzacji i limitów zasobów.

