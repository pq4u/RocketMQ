# Obsłuż ponowne dostarczenie i błędy

Ta instrukcja chroni handler przed skutkami dostarczenia tego samego komunikatu więcej niż raz.

## Użyj stabilnego identyfikatora

Zapisz <code>MessageId</code> razem z wynikiem operacji biznesowej. Przed wykonaniem efektu ubocznego sprawdź, czy ten identyfikator został już obsłużony:

~~~csharp
async Task<ConsumeResult> HandleAsync(
    MessageContext message,
    CancellationToken cancellationToken)
{
    if (await processedMessages.ContainsAsync(message.MessageId, cancellationToken))
    {
        return ConsumeResult.Success;
    }

    await orders.ApplyAsync(message.Payload, cancellationToken);
    await processedMessages.AddAsync(message.MessageId, cancellationToken);
    return ConsumeResult.Success;
}
~~~

Przechowywanie wyniku i identyfikatora w dwóch niezależnych transakcjach nadal zostawia okno awarii. Jeżeli system biznesowy na to pozwala, zapisz oba elementy atomowo.

## Dobierz czas widoczności

Ustaw <code>VisibilityTimeout</code> dłuższy niż typowy czas handlera:

~~~csharp
var options = new ConsumerOptions
{
    VisibilityTimeout = TimeSpan.FromMinutes(2)
};
~~~

SDK i serwer akceptują od jednej sekundy do jednej godziny. Projekt nie implementuje odnawiania dzierżawy.

## Klasyfikuj wynik handlera

- Zwróć <code>Success</code>, gdy efekt biznesowy został zapisany.
- Zwróć <code>Requeue</code>, gdy błąd jest przejściowy.
- Zwróć <code>DeadLetter</code>, gdy ponowienie nie może pomóc.

Niekończące się zwracanie <code>Requeue</code> nie omija limitu kolejki. Po osiągnięciu <code>MaxDeliveryCount</code> magazyn przenosi komunikat do dead letter.

## Następne kroki

- [Przeczytaj semantykę dostarczania](../explanation/semantyka-dostarczania.md).
- [Sprawdź błędy lease, Ack i Nack](../reference/bledy.md).
