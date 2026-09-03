# Raport techniczny RocketMQ

Raport opisuje projekt i implementację edukacyjnego systemu kolejkowania wiadomości RocketMQ. Jest warstwą narracyjną: prowadzi od problemu przez architekturę do oceny. Dokładne instrukcje i sygnatury pozostają w [dokumentacji użytkowej](../index.md).

## Zakres

Stan bazowy: bieżące drzewo robocze repozytorium, zweryfikowane 2 września 2026 na .NET SDK 10.0.400. Raport nie opisuje Apache RocketMQ i nie przedstawia prototypu jako systemu produkcyjnego.

## Rozdziały

1. [Wprowadzenie i cele](01-wprowadzenie.md)
2. [Podstawy techniczne](02-podstawy-techniczne.md)
3. [Wymagania i architektura](03-wymagania-i-architektura.md)
4. [Implementacja](04-implementacja.md)
5. [Weryfikacja](05-weryfikacja.md)
6. [Wydajność](06-wydajnosc.md)
7. [Ograniczenia i rozwój](07-ograniczenia.md)
8. [Rejestr dowodów](rejestr-dowodow.md)

## Zasady redakcyjne

Każde istotne twierdzenie o systemie powinno mieć odsyłacz do kodu, testu, kontraktu protobuf, ADR albo zewnętrznego źródła pierwotnego. Wynik pomiaru należy opisać wraz z commitem, konfiguracją i środowiskiem. Plany oznacza się jako plany; nie wolno przedstawiać ich jako zaimplementowanych funkcji.

