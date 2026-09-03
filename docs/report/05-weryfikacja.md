# 5. Weryfikacja

## Strategia

Weryfikacja łączy statyczne granice architektury, wspólne kontrakty adapterów, testy integracyjne SQLite, testy usług gRPC i kompilowalny przykład użytkownika. Każdy poziom odpowiada na inne ryzyko.

Testy architektury wykrywają niepożądane zależności. Kontrakty store opisują trwałość, atomowość lease, FIFO, Ack, Nack i współbieżność. Integracja SQLite sprawdza rzeczywisty schemat i ponowne otwarcie bazy. Testy gRPC kontrolują publiczne statusy. Przykład sprawdza, czy dokumentowana ścieżka SDK nadal się kompiluje.

## Ostatnia walidacja

Przed rozpoczęciem przebudowy dokumentacji pełny zestaw obejmował 97 zaliczonych testów: 7 architektury, 7 benchmarku, 11 transportu gRPC i 72 testy runnera/kontraktów zebrane przez projekty testowe. Po zmianach należy zawsze traktować wynik CI albo lokalnego polecenia jako nowsze źródło niż ten opis.

~~~powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build --verbosity normal
./tools/verify-docs.ps1
~~~

## Granice dowodu

Testy nie dowodzą odporności na każdą awarię sprzętu, poprawności działania na udziale sieciowym ani gotowości produkcyjnej. Nie ma testów klastra, bo klaster nie istnieje. Nie ma też zakończonego porównania własnego WAL z SQLite, ponieważ adapter WAL jest szkieletem.

## Znane ostrzeżenie zależności

Build zgłasza NU1903 dla <code>SQLitePCLRaw.lib.e_sqlite3</code> 2.1.11 i advisory GHSA-2m69-gcr7-jv3q. Dokumentacja nie uznaje go za rozwiązany problem. Aktualizacja zależności wymaga osobnej zmiany, testów migracji i ponownej walidacji.

