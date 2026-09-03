# Błędy i statusy

## Kody gRPC

| Kod | Przykładowa przyczyna |
|---|---|
| <code>InvalidArgument</code> | niepoprawne pola Publish, błędny UUID lease albo timeout poza zakresem |
| <code>NotFound</code> | brak exchange przy publikacji albo brak lease |
| <code>AlreadyExists</code> | ten sam PublishId użyty z inną treścią |
| <code>FailedPrecondition</code> | lease istniał, ale wygasł lub nie jest aktywny |
| <code>Cancelled</code> | anulowanie wywołania |
| <code>ResourceExhausted</code> | SDK potrafi ponowić; bieżący serwer nie emituje go dla pełnego publishera |

<code>Unroutable</code> jest statusem poprawnej odpowiedzi Publish, nie wyjątkiem gRPC.

Bieżący AdminService nie odrzuca nieznanego tekstu exchange_type: mapuje go na Direct. Jest to zachowanie implementacji, którego klient nie powinien wykorzystywać jako gwarantowanej normalizacji.

## Błędy uruchomienia

Runner kończy start przez <code>InvalidOperationException</code>, gdy ścieżka bazy jest pusta, względna, UNC lub bez katalogu, batch size nie jest dodatni albo batch delay jest ujemny lub ma niepoprawny format.

SQLite może zgłosić błędy wejścia-wyjścia, blokady albo naruszenia schematu. Operacja Publish nie powinna zostać uznana za przyjętą, jeśli commit się nie zakończył.
