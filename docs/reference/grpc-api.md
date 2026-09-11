# Referencja gRPC

Pakiet protobuf: <code>rocketmq.v1</code>. Wszystkie operacje są unary.

## Producer

<code>Publish(PublishRequest) → PublishResponse</code>

Request: <code>exchange_name</code>, <code>routing_key</code>, <code>payload</code>, opcjonalne <code>correlation_id</code>, opcjonalne UUID <code>publish_id</code> i <code>include_diagnostics</code>.

Response: <code>success</code>, UUID <code>message_id</code>, UUID <code>publish_id</code>, <code>status</code> (<code>Accepted</code> lub <code>Unroutable</code>), lista <code>destination_queues</code> i opcjonalne czasy diagnostyczne.

## Consumer

| RPC | Wejście | Wynik |
|---|---|---|
| <code>LeaseNext</code> | queue_name, visibility_timeout_seconds | lease_id, message_id, payload, delivery_count, correlation_id; puste lease_id oznacza brak wiadomości |
| <code>Ack</code> | lease_id UUID, opcjonalne queue_name | pusta odpowiedź po sukcesie |
| <code>Nack</code> | lease_id UUID, requeue, opcjonalne queue_name | pusta odpowiedź po sukcesie |

Timeout widoczności musi mieścić się od 1 do 3600 sekund.

Nowe SDK przekazuje <code>queue_name</code> w Ack/Nack, aby autoryzacja zasobowa nie wymagała dodatkowego odczytu. Pole jest addytywne i opcjonalne: dla starszego klienta broker odnajduje kolejkę aktywnego lease'a. Gdy autoryzacja jest włączona, lease należy do stabilnego <code>ClientId</code>; inny klient nie może go potwierdzić.

## Admin

| RPC | Pola |
|---|---|
| <code>DeclareExchange</code> | exchange_name, exchange_type: direct, fanout albo topic |
| <code>DeclareQueue</code> | queue_name |
| <code>Bind</code> | exchange_name, queue_name, routing_key |

Każda odpowiedź administracyjna zawiera <code>success</code>. Kanonicznym kontraktem jest [rocketmq.proto](../../src/Transport/RocketMQ.Transport.Grpc/Protos/rocketmq.proto).

DeclareQueue tworzy trwałą kolejkę z MaxDeliveryCount równym 10. Nieznany tekst exchange_type jest obecnie traktowany jak direct; to nieoczywiste zachowanie jest kandydatem do zaostrzenia walidacji.

Przy włączonej autoryzacji Publish wymaga dostępu do podanego exchange, Lease/Ack/Nack do kolejki, DeclareExchange do exchange, DeclareQueue do kolejki, a Bind do obu zasobów. Globalne role nadal obejmują wszystkie zasoby danej operacji.
