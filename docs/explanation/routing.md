# Routing

Router działa deterministycznie na topologii odczytanej przez <code>IRoutingStore</code>. Wynik jest zbiorem unikalnych nazw kolejek.

## Typy exchange

| Typ | Reguła |
|---|---|
| <code>Direct</code> | routing key musi być dokładnie równy kluczowi bindingu |
| <code>Fanout</code> | każda związana kolejka otrzymuje komunikat |
| <code>Topic</code> | routing key jest dopasowany do wzorca segmentowego |

W topic kropka rozdziela segmenty. Gwiazdka <code>*</code> dopasowuje dokładnie jeden segment, a hash <code>#</code> zero lub więcej segmentów. <code>orders.*</code> pasuje do <code>orders.created</code>, lecz nie do <code>orders.eu.created</code>. <code>orders.#</code> pasuje do obu.

## Brak dopasowania

Publikacja do istniejącego exchange bez pasującego bindingu ma status <code>Unroutable</code>. Nie jest błędem transportowym: klient dostaje poprawną odpowiedź z pustą listą kolejek. Publikacja do nieistniejącego exchange kończy się gRPC <code>NotFound</code>.

## Deduplikacja i braki

Jeśli kilka bindingów prowadzi do tej samej kolejki, router zwraca ją tylko raz. Kod nie implementuje specjalnego exchange o pustej nazwie ani automatycznego bindingu kolejki do własnej nazwy. Taki pomysł występuje w otwartych dokumentach projektowych, lecz nie jest częścią bieżącego zachowania.

