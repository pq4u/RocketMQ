# 6. Wydajność

## Co mierzy narzędzie

Benchmark wykonuje zamkniętą pętlę równoległych publikacji gRPC do działającego brokera. Mierzy tylko zakończone operacje Publish, czyli ścieżkę do trwałego commit. Nie konsumuje wiadomości.

Raport JSON zawiera throughput, percentyle opóźnienia, liczbę Accepted, Unroutable i błędów, rozmiar plików SQLite oraz opis środowiska. Tryb detailed timings rozdziela czas klienta/transportu, oczekiwanie na writera, pracę transakcji, commit i składanie batcha.

## Hipoteza

Batching powinien zwiększać przepustowość przy współbieżności, ponieważ kilka publikacji współdzieli transakcję, ale może podnieść opóźnienie pojedynczego żądania o czas oczekiwania na batch. Większy fanout zwiększa liczbę insertów na publikację. To hipotezy do pomiaru, nie wyniki.

## Procedura porównania

Każdą serię należy wykonać co najmniej trzykrotnie na świeżej bazie, po stałej rozgrzewce, z niezmienionym sprzętem i konfiguracją. Raport powinien zawierać commit, argumenty brokera i benchmarku oraz p50, p95 i p99. CPU i pamięć trzeba zebrać osobnym profilerem.

## Stan wyników

Repozytorium zawiera narzędzie i plan pomiaru, ale ten raport nie wpisuje liczb bez zweryfikowanych artefaktów z porównywalnych uruchomień. Referencja argumentów znajduje się w [benchmark CLI](../reference/benchmark-cli.md).

