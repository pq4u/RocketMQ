# Słownik

| Termin | Znaczenie w projekcie |
|---|---|
| Ack | pozytywne potwierdzenie kończące obsługę |
| adapter | implementacja portu Core, np. SQLite albo gRPC |
| at-least-once | komunikat może być dostarczony więcej niż raz |
| backpressure | spowolnienie producenta, gdy odbiorca pracy nie nadąża |
| batch | grupa publikacji zapisana w jednej transakcji |
| binding | reguła łącząca exchange z kolejką |
| Channel | ulotny, asynchroniczny bufor producent-konsument w procesie .NET |
| dead-letter | wiadomość odsunięta od zwykłego dostarczania |
| DI | dependency injection, dostarczanie zależności przez kontener |
| exchange | punkt wejścia wybierający kolejki na podstawie routingu |
| handler | kod aplikacji przetwarzający wydzierżawiony komunikat |
| idempotencja | wielokrotne wykonanie tego samego żądania bez dodatkowego efektu |
| lease | czasowe prawo do obsługi konkretnego dostarczenia |
| Nack | negatywne potwierdzenie: requeue albo dead-letter |
| port | interfejs w Core opisujący wymaganą usługę |
| protobuf | język kontraktu i binarny format wiadomości gRPC |
| redelivery | ponowne dostarczenie po Nack lub wygaśnięciu lease |
| routing key | klucz publikacji porównywany z bindingiem |
| visibility timeout | czas, przez który wydzierżawiona wiadomość jest ukryta |
| WAL | write-ahead log; w SQLite tryb dziennika, a w projekcie także nazwa niedokończonego adaptera |

