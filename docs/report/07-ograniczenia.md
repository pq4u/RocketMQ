# 7. Ograniczenia i kierunki rozwoju

## Ograniczenia bieżące

- pojedynczy proces i jeden lokalny plik SQLite;
- mTLS jest opcjonalne, a domyślny TLS nadal nie uwierzytelnia klienta;
- ACL exchange/kolejek używa statycznych, dokładnych nazw: brak wildcardów, prefiksów, tenantów i przeładowania bez restartu;
- brak replikacji, HA i recovery między węzłami;
- unary polling zamiast streamingu;
- brak automatycznego odnawiania lease;
- brak publicznego browse i replay dead letters;
- brak konfigurowalnej retencji i logowania błędów maintenance;
- adapter WAL składa się z jawnych stubów;
- otwarte ostrzeżenie bezpieczeństwa zależności SQLite.

## Priorytety rozwoju

TLS i domyślny endpoint są ujednolicone, opcjonalne mTLS zapewnia uwierzytelnienie certyfikatu klienta, a allowlista może ograniczyć grupy operacji i dokładne nazwy zasobów. Następne ryzyka bezpieczeństwa to podatna zależność, limity per klient, automatyczna rotacja certyfikatów, dynamiczne zarządzanie ACL i trwały audyt; potrzebna jest również obserwowalność. Następnie można rozszerzyć operacje administracyjne i cykl lease. Klaster albo własny WAL wymaga osobnej decyzji architektonicznej i testów awaryjnych; nie powinien być dopisywany jako drobny adapter.

## Otwarte decyzje

Dokumenty w [docs/decisions](../decisions/) zachowują pytania o domyślne zachowanie topologii, limity, idempotencję, bezpieczeństwo i retencję. Zaobserwowane zachowanie kodu nie oznacza automatycznego zatwierdzenia decyzji produktowej.

## Wniosek

Projekt demonstruje spójny rdzeń brokera: trwałą publikację, routing i lease z at-least-once. Największą wartością jest jawny kontrakt zachowania oraz oddzielenie portów od adapterów. Najważniejszą granicą jest natomiast brak cech operacyjnych wymaganych w publicznej usłudze.
