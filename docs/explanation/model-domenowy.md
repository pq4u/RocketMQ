# Model domenowy

Model domenowy oddziela treść komunikatu od stanu jego dostarczania.

## Publikowany komunikat

<code>InboundMessage</code> zawiera exchange, routing key, opcjonalny <code>CorrelationId</code>, surowy payload i znacznik czasu. <code>Envelope</code> uzupełnia go o stabilny <code>MessageId</code>. Kompozycja pozwala zachować niezmienność wejściowego rekordu.

## Topologia

<code>Exchange</code> ma nazwę i typ: <code>Direct</code>, <code>Fanout</code> albo <code>Topic</code>. <code>QueueDefinition</code> opisuje kolejkę, a <code>Binding</code> łączy ją z exchange i przechowuje klucz lub wzorzec.

Ponowne zadeklarowanie obiektu z tymi samymi parametrami jest idempotentne; konflikt parametrów jest błędem.

## Dostarczenie

<code>LeasedMessage</code> zawiera stabilny <code>MessageId</code>, unikalny dla dzierżawy <code>LeaseId</code>, termin widoczności i <code>DeliveryCount</code>. Ponowne dostarczenie zachowuje MessageId, ale otrzymuje nowy lease.

<code>DeadLetteredMessage</code> zachowuje oryginalny komunikat wraz z informacją diagnostyczną. W obecnym API nie istnieje operacja administracyjna do jego odczytu.

## Identyfikatory o różnych rolach

| Identyfikator | Znaczenie |
|---|---|
| <code>PublishId</code> | klucz idempotencji pojedynczej publikacji |
| <code>MessageId</code> | stabilna tożsamość kopii w kolejce |
| <code>LeaseId</code> | uprawnienie do Ack lub Nack konkretnej dzierżawy |
| <code>CorrelationId</code> | opcjonalny identyfikator biznesowy klienta |

Nie należy używać CorrelationId jako zamiennika PublishId. Pierwszy pomaga śledzić proces biznesowy, drugi chroni konkretną publikację.

