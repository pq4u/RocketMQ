# Semantyka dostarczania

RocketMQ zapewnia dostarczanie **co najmniej raz**. Gwarancja dotyczy zachowania store, a nie dokładnie jednokrotnego wykonania kodu użytkownika.

~~~mermaid
stateDiagram-v2
    [*] --> Available: enqueue
    Available --> Leased: LeaseNext
    Leased --> [*]: Ack
    Leased --> Available: Nack requeue=true
    Leased --> Available: visibility timeout
    Leased --> DeadLetter: Nack requeue=false
~~~

## Lease i visibility timeout

<code>LeaseNext</code> wybiera najstarszy dostępny komunikat i atomowo ukrywa go przed innymi konsumentami do podanego terminu. Zakres timeoutu w API wynosi od 1 sekundy do 1 godziny; SDK domyślnie używa 30 sekund.

Gdy handler nie potwierdzi komunikatu przed upływem czasu, następne odpytywanie może go wydzierżawić ponownie. SDK nie odnawia lease automatycznie, dlatego długie operacje wymagają odpowiednio dużego timeoutu.

## Ack, Nack i licznik

<code>Ack</code> wymaga aktualnego LeaseId i trwale usuwa wiadomość. <code>Nack(requeue: true)</code> natychmiast przywraca dostępność, a <code>Nack(requeue: false)</code> przenosi wpis do dead-letter. Stary, błędny lub wygasły lease nie może potwierdzić nowszej dzierżawy.

<code>DeliveryCount</code> jest zwiększany przy każdej udanej dzierżawie, również pierwszej. Kolejki deklarowane przez publiczne Admin API otrzymują MaxDeliveryCount równe 10. Po wykorzystaniu limitu store przenosi komunikat do dead-letter z powodem <code>max-delivery-count-exceeded</code>; wartość 0 w modelu oznacza brak limitu.

## Konsekwencja dla aplikacji

Handler powinien być idempotentny. Typowy wzorzec zapisuje MessageId razem z wynikiem operacji biznesowej w jednej transakcji aplikacyjnej. Ack następuje dopiero po trwałym zakończeniu pracy. Zobacz instrukcję [obsługi redelivery](../how-to/obsluz-redelivery.md).
