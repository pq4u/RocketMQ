# Uruchom broker z SQLite

Ta instrukcja uruchamia jedną instancję brokera z bazą SQLite na lokalnym dysku.

## Przygotuj środowisko

Zainstaluj .NET 10 SDK. W katalogu głównym repozytorium wykonaj:

~~~powershell
dotnet restore
dotnet build --no-restore
~~~

## Uruchom proces

Utwórz bezwzględną ścieżkę bazy i przekaż ją jako argument konfiguracji:

~~~powershell
$databasePath = Join-Path (Get-Location) ".data\rocketmq.db"
dotnet run --project src/Runner/RocketMQ.Runner --no-build -- --RocketMQ:Persistence:DatabasePath=$databasePath
~~~

Ścieżka musi wskazywać lokalny system plików, zawierać nazwę katalogu i nie może być ścieżką UNC. Runner tworzy brakujący katalog.

## Sprawdź uruchomienie

Proces powinien pozostać aktywny i nasłuchiwać na porcie <code>50051</code>. Serwer używa HTTP/2 bez TLS i wiąże port do wszystkich interfejsów.

> **Ostrzeżenie:** uruchamiaj ten wariant wyłącznie w zaufanym środowisku lokalnym. Broker nie uwierzytelnia klientów.

## Zatrzymaj proces

Naciśnij <code>Ctrl+C</code>. Nie kopiuj samego pliku bazy podczas aktywnego zapisu: w trybie WAL pliki <code>-wal</code> i <code>-shm</code> należą do stanu SQLite.

## Następne kroki

- [Skonfiguruj persistence](skonfiguruj-persistence.md).
- [Wyślij pierwszy komunikat](../tutorials/pierwszy-komunikat.md).
- [Sprawdź ograniczenia wdrożeniowe](../reference/status-funkcji.md).
