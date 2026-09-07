# Skonfiguruj wzajemne TLS

mTLS powoduje, że broker nie tylko przedstawia swój certyfikat TLS, ale również żąda certyfikatu od klienta. W RocketMQ jest to funkcja opcjonalna. Sama walidacja mTLS dopuszcza każdy certyfikat podpisany przez skonfigurowane CA. Opcjonalna autoryzacja mapuje fingerprint SHA-256 certyfikatu na klienta i niezależne uprawnienia <code>Publish</code>, <code>Consume</code> oraz <code>Admin</code>.

## Utwórz lokalne CA i certyfikat klienta

Poniższy przykład wymaga OpenSSL. Certyfikaty produkcyjne powinien wystawiać operator PKI, a nie sama aplikacja kliencka.

~~~powershell
$certDir = Join-Path (Get-Location) ".data\certs"
New-Item -ItemType Directory -Force -Path $certDir | Out-Null

openssl req -x509 -newkey rsa:3072 -sha256 -days 3650 -nodes `
  -keyout "$certDir\client-ca.key" `
  -out "$certDir\client-ca.pem" `
  -subj "/CN=RocketMQ Development Client CA" `
  -addext "basicConstraints=critical,CA:TRUE" `
  -addext "keyUsage=critical,keyCertSign,cRLSign"

openssl req -newkey rsa:2048 -nodes `
  -keyout "$certDir\client.key" `
  -out "$certDir\client.csr" `
  -subj "/CN=rocketmq-example"

@"
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature
extendedKeyUsage=clientAuth
"@ | Set-Content "$certDir\client.ext" -Encoding ascii

openssl x509 -req -sha256 -days 30 `
  -in "$certDir\client.csr" `
  -CA "$certDir\client-ca.pem" `
  -CAkey "$certDir\client-ca.key" `
  -CAcreateserial `
  -extfile "$certDir\client.ext" `
  -out "$certDir\client.pem"

$env:ROCKETMQ_CLIENT_CERTIFICATE_PASSWORD = "local-test-password"
openssl pkcs12 -export `
  -out "$certDir\client.pfx" `
  -inkey "$certDir\client.key" `
  -in "$certDir\client.pem" `
  -certfile "$certDir\client-ca.pem" `
  -passout env:ROCKETMQ_CLIENT_CERTIFICATE_PASSWORD
~~~

Klucz CA i klucz klienta są sekretami. Nie umieszczaj katalogu z certyfikatami w repozytorium.

## Uruchom broker z mTLS

Certyfikat serwera nadal pochodzi ze standardowej konfiguracji Kestrela. Dodatkowo włącz mTLS i wskaż publiczny certyfikat CA klientów:

~~~powershell
$databasePath = Join-Path (Get-Location) ".data\rocketmq-mtls.db"
$env:RocketMQ__Security__MutualTls__Enabled = "true"
$env:RocketMQ__Security__MutualTls__TrustedClientCaPath = "$certDir\client-ca.pem"
dotnet run --project src/Runner/RocketMQ.Runner -- --RocketMQ:Persistence:DatabasePath=$databasePath
~~~

Po włączeniu mTLS wszystkie endpointy muszą używać HTTPS. Brak CA, endpoint HTTP albo słabsze ustawienie <code>ClientCertificateMode</code> zatrzyma start brokera.

## Włącz autoryzację operacji

Oblicz fingerprint certyfikatu klienta. OpenSSL zwraca format z dwukropkami, który Runner akceptuje:

~~~powershell
$fingerprintLine = openssl x509 -in "$certDirclient.pem" -noout -fingerprint -sha256
$fingerprint = ($fingerprintLine -split '=', 2)[1]
~~~

Następnie włącz allowlistę i przypisz klientowi uprawnienia. Pełny przykład SDK wykonuje operacje administracyjne, publikuje i konsumuje, dlatego wymaga wszystkich trzech:

~~~powershell
$env:RocketMQ__Security__Authorization__Enabled = "true"
$env:RocketMQ__Security__Authorization__Clients__rocketmq_example__CertificateSha256Fingerprints__0 = $fingerprint
$env:RocketMQ__Security__Authorization__Clients__rocketmq_example__Permissions__0 = "Publish"
$env:RocketMQ__Security__Authorization__Clients__rocketmq_example__Permissions__1 = "Consume"
$env:RocketMQ__Security__Authorization__Clients__rocketmq_example__Permissions__2 = "Admin"
~~~

Autoryzacja wymaga włączonego mTLS. Nieznany fingerprint powoduje gRPC <code>Unauthenticated</code>, a rozpoznany klient bez wymaganej roli otrzymuje <code>PermissionDenied</code>. Uprawnienia są niezależne: <code>Admin</code> nie daje automatycznie <code>Publish</code> ani <code>Consume</code>.

## Połącz przykład lub benchmark

Przykład SDK odczytuje certyfikat ze zmiennych środowiskowych:

~~~powershell
$env:ROCKETMQ_CLIENT_CERTIFICATE_PATH = "$certDir\client.pfx"
dotnet run --project examples/RocketMQ.Example
~~~

Benchmark przyjmuje ścieżkę PFX, ale nazwę zmiennej z hasłem przekazuje osobno, aby sekret nie trafiał do argumentów procesu. Przy włączonej autoryzacji jego certyfikat wymaga <code>Admin</code> oraz <code>Publish</code>, ponieważ narzędzie tworzy topologię i publikuje wiadomości:

~~~powershell
dotnet run --project tools/RocketMQ.Benchmark -- `
  --endpoint https://localhost:50051 `
  --database-path $databasePath `
  --client-certificate-path "$certDir\client.pfx" `
  --client-certificate-password-env ROCKETMQ_CLIENT_CERTIFICATE_PASSWORD
~~~

Można również przekazać certyfikat PEM wraz z <code>ClientCertificateKeyPath</code> w SDK albo opcją <code>--client-certificate-key-path</code> benchmarku.

Walidacja łańcucha klienta jest wykonywana podczas zestawiania połączenia TLS. Kanał HTTP/2 jest później używany wielokrotnie, więc certyfikat nie jest ponownie wysyłany i walidowany dla każdego RPC. Niepoprawny certyfikat przerywa połączenie przed obsługą RPC i zwykle jest widoczny po stronie klienta jako gRPC <code>Unavailable</code> z błędem TLS.

Zmiana CA, certyfikatu klienta lub allowlisty wymaga restartu procesu i utworzenia nowych połączeń. Certyfikat można obrócić bez przestoju klienta przez tymczasowe umieszczenie starego i nowego fingerprintu w tablicy <code>CertificateSha256Fingerprints</code> tego samego <code>ClientId</code>.
