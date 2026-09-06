# Uruchom broker z SQLite

Ta instrukcja uruchamia jedną instancję brokera z bazą SQLite na lokalnym dysku.

## Przygotuj środowisko

Zainstaluj .NET 10 SDK. W katalogu głównym repozytorium wykonaj:

~~~powershell
dotnet restore
dotnet build --no-restore
dotnet dev-certs https --trust
~~~

Ostatnie polecenie tworzy lub zatwierdza lokalny certyfikat deweloperski używany przez domyślny endpoint HTTPS.

## Uruchom proces

Utwórz bezwzględną ścieżkę bazy i przekaż ją jako argument konfiguracji:

~~~powershell
$databasePath = Join-Path (Get-Location) ".data\rocketmq.db"
dotnet run --project src/Runner/RocketMQ.Runner --no-build -- --RocketMQ:Persistence:DatabasePath=$databasePath
~~~

Ścieżka musi wskazywać lokalny system plików, zawierać nazwę katalogu i nie może być ścieżką UNC. Runner tworzy brakujący katalog.

## Sprawdź uruchomienie

Proces powinien pozostać aktywny i nasłuchiwać pod adresem <code>https://localhost:50051</code>. Serwer używa HTTP/2 z TLS i domyślnie wiąże port tylko do loopback.

> **Ostrzeżenie:** TLS chroni transmisję, ale broker nadal nie uwierzytelnia ani nie autoryzuje klientów.

Jawny tryb nieszyfrowany jest dostępny wyłącznie na loopback:

~~~powershell
dotnet run --project src/Runner/RocketMQ.Runner --no-build -- --RocketMQ:Persistence:DatabasePath=$databasePath --Kestrel:Endpoints:Grpc:Url=http://localhost:50051
~~~

Runner odrzuci konfigurację HTTP wskazującą adres inny niż loopback. Wystawienie brokera w sieci wymaga endpointu HTTPS i produkcyjnego certyfikatu opisanego w [referencji konfiguracji](../reference/konfiguracja.md).

## Zatrzymaj proces

Naciśnij <code>Ctrl+C</code>. Nie kopiuj samego pliku bazy podczas aktywnego zapisu: w trybie WAL pliki <code>-wal</code> i <code>-shm</code> należą do stanu SQLite.

## Następne kroki

- [Skonfiguruj persistence](skonfiguruj-persistence.md).
- [Wyślij pierwszy komunikat](../tutorials/pierwszy-komunikat.md).
- [Sprawdź ograniczenia wdrożeniowe](../reference/status-funkcji.md).
