# 1. Wprowadzenie i cele

System kolejkowania oddziela tempo i czas życia producenta od konsumenta. Producent przekazuje dane brokerowi, a odbiorca może przetworzyć je później. Trudność nie polega tylko na przesłaniu bajtów: trzeba jednoznacznie określić routing, moment uznania zapisu za trwały, zachowanie po awarii i reguły potwierdzania.

## Cel projektu

Celem RocketMQ jest zaprojektowanie i zaimplementowanie niewielkiego brokera, który demonstruje:

- routing direct, fanout i topic;
- trwałe publikowanie do wielu kolejek;
- konkurujących konsumentów z lease i visibility timeout;
- semantykę co najmniej raz, Ack, Nack i dead-letter;
- idempotencję publikacji;
- szyfrowanie transportu gRPC przez TLS;
- opcjonalne uwierzytelnianie klientów przez mTLS;
- rozdzielenie domeny od gRPC i SQLite.

## Pytania techniczne

Projekt odpowiada na trzy pytania. Jak zachować niezależny model domenowy przy konkretnym transporcie? Jak połączyć równoległe żądania sieciowe z ograniczeniem jednego writera SQLite? Jak określić dostarczenie tak, aby awaria konsumenta nie usuwała pracy?

## Kryteria oceny

Ocena nie opiera się wyłącznie na uruchomieniu dema. Kryteriami są kontrakty Core, automatyczne testy różnych warstw, zgodność dokumentacji z kodem i powtarzalny benchmark. Rejestr wskazujący dowody znajduje się w [rejestrze dowodów](rejestr-dowodow.md).

## Poza zakresem

Prototyp nie realizuje klastra, replikacji, autoryzacji ani dokładnie jednokrotnego przetwarzania. Domyślny TLS nie identyfikuje klienta, natomiast opcjonalne mTLS dopuszcza tylko certyfikaty prowadzące do skonfigurowanego prywatnego CA. Nie mapuje ich jeszcze na role ani uprawnienia. Nie ukończono również własnego adaptera WAL.
