using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Transport.Grpc.Tests;

public sealed class GrpcTransportServerTests
{
    [Fact]
    public async Task StartAsync_WithHttpsEndpoint_NegotiatesTlsAndHttp2()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "RocketMQ.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var certificatePath = Path.Combine(tempDirectory, "server.pfx");
        const string certificatePassword = "test-password";
        using var certificate = CreateServerCertificate();
        await File.WriteAllBytesAsync(
            certificatePath,
            certificate.Export(X509ContentType.Pfx, certificatePassword),
            TestContext.Current.CancellationToken);

        var port = GetAvailablePort();
        var configuration = CreateConfiguration($"https://127.0.0.1:{port}");
        configuration["Kestrel:Endpoints:Grpc:Certificate:Path"] = certificatePath;
        configuration["Kestrel:Endpoints:Grpc:Certificate:Password"] = certificatePassword;
        var server = CreateServer(configuration);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await server.StartAsync(timeout.Token);
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
            using var sslStream = new SslStream(
                tcpClient.GetStream(),
                false,
                (_, presentedCertificate, _, _) =>
                    presentedCertificate?.GetCertHashString() == certificate.GetCertHashString());
            await sslStream.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions
                {
                    TargetHost = "localhost",
                    ApplicationProtocols = [SslApplicationProtocol.Http2],
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                },
                timeout.Token);

            Assert.Equal(SslApplicationProtocol.Http2, sslStream.NegotiatedApplicationProtocol);
            Assert.Contains(
                sslStream.SslProtocol,
                new[] { SslProtocols.Tls12, SslProtocols.Tls13 });
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task StartAsync_WithLoopbackHttpEndpoint_StartsExplicitInsecureMode()
    {
        var port = GetAvailablePort();
        var server = CreateServer(CreateConfiguration($"http://127.0.0.1:{port}"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await server.StartAsync(timeout.Token);
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAsync_WithMutualTls_AcceptsTrustedClientCertificate(bool usePemCa)
    {
        using var clientCa = CreateCertificateAuthority("CN=RocketMQ trusted client CA");
        using var clientCertificate = CreateIssuedCertificate(
            clientCa,
            "CN=trusted-client",
            "1.3.6.1.5.5.7.3.2",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));

        await RunMutualTlsHandshakeAsync(
            clientCa,
            clientCertificate,
            shouldSucceed: true,
            usePemCa: usePemCa);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("untrusted")]
    [InlineData("expired")]
    [InlineData("server-auth")]
    public async Task StartAsync_WithMutualTls_RejectsInvalidClientCertificate(string certificateKind)
    {
        using var trustedCa = CreateCertificateAuthority("CN=RocketMQ trusted client CA");
        using var untrustedCa = certificateKind == "untrusted"
            ? CreateCertificateAuthority("CN=RocketMQ untrusted client CA")
            : null;
        using var clientCertificate = certificateKind switch
        {
            "missing" => null,
            "untrusted" => CreateIssuedCertificate(
                untrustedCa!,
                "CN=untrusted-client",
                "1.3.6.1.5.5.7.3.2",
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddDays(1)),
            "expired" => CreateIssuedCertificate(
                trustedCa,
                "CN=expired-client",
                "1.3.6.1.5.5.7.3.2",
                DateTimeOffset.UtcNow.AddDays(-2),
                DateTimeOffset.UtcNow.AddDays(-1)),
            "server-auth" => CreateIssuedCertificate(
                trustedCa,
                "CN=wrong-purpose-client",
                "1.3.6.1.5.5.7.3.1",
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddDays(1)),
            _ => throw new ArgumentOutOfRangeException(nameof(certificateKind))
        };

        await RunMutualTlsHandshakeAsync(trustedCa, clientCertificate, shouldSucceed: false);
    }

    [Fact]
    public async Task StartAsync_WithMutualTlsAndHttpEndpoint_RejectsConfiguration()
    {
        var configuration = CreateConfiguration($"http://127.0.0.1:{GetAvailablePort()}");
        configuration["RocketMQ:Security:MutualTls:Enabled"] = "true";
        var server = CreateServer(configuration);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.StartAsync(CancellationToken.None));

        Assert.Contains("must use HTTPS when mutual TLS is enabled", exception.Message);
    }

    [Fact]
    public async Task StartAsync_WithMutualTlsAndMissingTrustedCa_RejectsConfiguration()
    {
        var configuration = CreateConfiguration($"https://127.0.0.1:{GetAvailablePort()}");
        configuration["RocketMQ:Security:MutualTls:Enabled"] = "true";
        var server = CreateServer(configuration);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.StartAsync(CancellationToken.None));

        Assert.Contains("TrustedClientCaPath", exception.Message);
    }

    [Fact]
    public async Task StartAsync_WithMutualTlsAndWeakerKestrelMode_RejectsConfiguration()
    {
        var configuration = CreateConfiguration($"https://127.0.0.1:{GetAvailablePort()}");
        configuration["RocketMQ:Security:MutualTls:Enabled"] = "true";
        configuration["Kestrel:Endpoints:Grpc:ClientCertificateMode"] = "NoCertificate";
        var server = CreateServer(configuration);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.StartAsync(CancellationToken.None));

        Assert.Contains("ClientCertificateMode RequireCertificate", exception.Message);
    }

    [Fact]
    public async Task StartAsync_WithMutualTlsAndNonCaTrustFile_RejectsConfiguration()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "RocketMQ.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var trustPath = Path.Combine(tempDirectory, "not-a-ca.cer");
        using var certificate = CreateServerCertificate();
        await File.WriteAllBytesAsync(
            trustPath,
            certificate.Export(X509ContentType.Cert),
            TestContext.Current.CancellationToken);
        var configuration = CreateConfiguration($"https://127.0.0.1:{GetAvailablePort()}");
        configuration["RocketMQ:Security:MutualTls:Enabled"] = "true";
        configuration["RocketMQ:Security:MutualTls:TrustedClientCaPath"] = trustPath;
        var server = CreateServer(configuration);

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => server.StartAsync(CancellationToken.None));

            Assert.Contains("is not a CA certificate", exception.Message);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task StartAsync_WithInvalidRevocationMode_RejectsConfiguration()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "RocketMQ.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var trustPath = Path.Combine(tempDirectory, "client-ca.cer");
        using var clientCa = CreateCertificateAuthority("CN=RocketMQ trusted client CA");
        await File.WriteAllBytesAsync(
            trustPath,
            clientCa.Export(X509ContentType.Cert),
            TestContext.Current.CancellationToken);
        var configuration = CreateConfiguration($"https://127.0.0.1:{GetAvailablePort()}");
        configuration["RocketMQ:Security:MutualTls:Enabled"] = "true";
        configuration["RocketMQ:Security:MutualTls:TrustedClientCaPath"] = trustPath;
        configuration["RocketMQ:Security:MutualTls:RevocationMode"] = "Sometimes";
        var server = CreateServer(configuration);

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => server.StartAsync(CancellationToken.None));

            Assert.Contains("must be NoCheck, Offline, or Online", exception.Message);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Theory]
    [InlineData("http://0.0.0.0:50051", "Http2", "must use HTTPS")]
    [InlineData("ftp://localhost:50051", "Http2", "must use HTTPS")]
    [InlineData("/grpc", "Http2", "absolute HTTP(S) URI")]
    [InlineData("https://localhost:50051", "Http1", "must use the Http2 protocol")]
    public async Task StartAsync_WithUnsafeOrInvalidEndpoint_RejectsConfiguration(
        string url,
        string protocols,
        string expectedMessage)
    {
        var server = CreateServer(CreateConfiguration(url, protocols));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.StartAsync(CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task StartAsync_WithMissingCertificate_FailsInsteadOfFallingBackToHttp()
    {
        var configuration = CreateConfiguration($"https://127.0.0.1:{GetAvailablePort()}");
        configuration["Kestrel:Endpoints:Grpc:Certificate:Path"] =
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.pfx");
        var server = CreateServer(configuration);

        var exception = await Record.ExceptionAsync(() => server.StartAsync(CancellationToken.None));

        Assert.NotNull(exception);
    }

    [Fact]
    public async Task StartAsync_WithAuthorizationWithoutMutualTls_RejectsConfiguration()
    {
        var configuration = CreateConfiguration($"https://127.0.0.1:{GetAvailablePort()}");
        configuration["RocketMQ:Security:Authorization:Enabled"] = "true";
        var server = CreateServer(configuration);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.StartAsync(CancellationToken.None));

        Assert.Contains("requires RocketMQ:Security:MutualTls:Enabled=true", exception.Message);
    }

    [Theory]
    [InlineData("Publish", "Publish")]
    [InlineData("Consume", "LeaseNext")]
    [InlineData("Consume", "Ack")]
    [InlineData("Consume", "Nack")]
    [InlineData("Admin", "DeclareExchange")]
    [InlineData("Admin", "DeclareQueue")]
    [InlineData("Admin", "Bind")]
    public async Task Rpc_WithConfiguredPermission_IsAllowed(
        string permission,
        string rpc)
    {
        await RunAuthorizationRpcScenarioAsync(
            permission,
            rpc,
            registerPresentedCertificate: true,
            authorizationEnabled: true,
            expectedStatus: null);
    }

    [Theory]
    [InlineData("Publish", "LeaseNext")]
    [InlineData("Consume", "DeclareQueue")]
    [InlineData("Admin", "Publish")]
    public async Task Rpc_WithoutConfiguredPermission_ReturnsPermissionDenied(
        string permission,
        string rpc)
    {
        await RunAuthorizationRpcScenarioAsync(
            permission,
            rpc,
            registerPresentedCertificate: true,
            authorizationEnabled: true,
            expectedStatus: StatusCode.PermissionDenied);
    }

    [Fact]
    public async Task Rpc_WithUnregisteredTrustedCertificate_ReturnsUnauthenticated()
    {
        await RunAuthorizationRpcScenarioAsync(
            "Publish",
            "Publish",
            registerPresentedCertificate: false,
            authorizationEnabled: true,
            expectedStatus: StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task Rpc_WithAuthorizationDisabled_PreservesMutualTlsAccess()
    {
        await RunAuthorizationRpcScenarioAsync(
            "Publish",
            "Publish",
            registerPresentedCertificate: false,
            authorizationEnabled: false,
            expectedStatus: null);
    }

    private static GrpcTransportServer CreateServer(
        IConfiguration configuration,
        IMessagePublisher? publisher = null,
        IMessageQueueStore? queueStore = null,
        IRoutingStore? routingStore = null)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(Environments.Development);
        environment.SetupGet(value => value.ContentRootPath).Returns(Directory.GetCurrentDirectory());
        return new GrpcTransportServer(
            publisher ?? Mock.Of<IMessagePublisher>(),
            queueStore ?? Mock.Of<IMessageQueueStore>(),
            routingStore ?? Mock.Of<IRoutingStore>(),
            configuration,
            environment.Object);
    }

    private static ConfigurationManager CreateConfiguration(string url, string protocols = "Http2")
    {
        var configuration = new ConfigurationManager();
        configuration["Kestrel:Endpoints:Grpc:Url"] = url;
        configuration["Kestrel:Endpoints:Grpc:Protocols"] = protocols;
        return configuration;
    }

    private static X509Certificate2 CreateServerCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        var enhancedKeyUsages = new OidCollection
        {
            new("1.3.6.1.5.5.7.3.1")
        };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(enhancedKeyUsages, false));
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());

        using var generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(
            generated.Export(X509ContentType.Pfx),
            null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }

    private static X509Certificate2 CreateCertificateAuthority(string subject)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            subject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        using var generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-3),
            DateTimeOffset.UtcNow.AddDays(3));
        return X509CertificateLoader.LoadPkcs12(
            generated.Export(X509ContentType.Pfx),
            null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }

    private static X509Certificate2 CreateIssuedCertificate(
        X509Certificate2 issuer,
        string subject,
        string enhancedKeyUsage,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            subject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(new OidCollection { new(enhancedKeyUsage) }, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var serialNumber = RandomNumberGenerator.GetBytes(16);
        serialNumber[0] &= 0x7f;

        using var issued = request.Create(issuer, notBefore, notAfter, serialNumber);
        using var issuedWithPrivateKey = issued.CopyWithPrivateKey(rsa);
        return X509CertificateLoader.LoadPkcs12(
            issuedWithPrivateKey.Export(X509ContentType.Pfx),
            null,
            X509KeyStorageFlags.Exportable);
    }

    private static async Task RunAuthorizationRpcScenarioAsync(
        string permission,
        string rpc,
        bool registerPresentedCertificate,
        bool authorizationEnabled,
        StatusCode? expectedStatus)
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "RocketMQ.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var serverCertificatePath = Path.Combine(tempDirectory, "server.pfx");
        var clientCaPath = Path.Combine(tempDirectory, "client-ca.cer");
        const string serverCertificatePassword = "test-password";
        using var clientCa = CreateCertificateAuthority("CN=RocketMQ authorization client CA");
        using var presentedCertificate = CreateIssuedCertificate(
            clientCa,
            "CN=presented-client",
            "1.3.6.1.5.5.7.3.2",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
        using var registeredCertificate = registerPresentedCertificate
            ? null
            : CreateIssuedCertificate(
                clientCa,
                "CN=registered-client",
                "1.3.6.1.5.5.7.3.2",
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddDays(1));
        using var serverCertificate = CreateServerCertificate();
        await File.WriteAllBytesAsync(
            serverCertificatePath,
            serverCertificate.Export(X509ContentType.Pfx, serverCertificatePassword),
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            clientCaPath,
            clientCa.Export(X509ContentType.Cert),
            TestContext.Current.CancellationToken);

        var port = GetAvailablePort();
        var configuration = CreateConfiguration($"https://127.0.0.1:{port}");
        configuration["Kestrel:Endpoints:Grpc:Certificate:Path"] = serverCertificatePath;
        configuration["Kestrel:Endpoints:Grpc:Certificate:Password"] = serverCertificatePassword;
        configuration["RocketMQ:Security:MutualTls:Enabled"] = "true";
        configuration["RocketMQ:Security:MutualTls:TrustedClientCaPath"] = clientCaPath;
        configuration["RocketMQ:Security:Authorization:Enabled"] = authorizationEnabled.ToString();
        if (authorizationEnabled)
        {
            var configuredCertificate = registeredCertificate ?? presentedCertificate;
            configuration[
                "RocketMQ:Security:Authorization:Clients:test-client:CertificateSha256Fingerprints:0"] =
                configuredCertificate.GetCertHashString(HashAlgorithmName.SHA256);
            configuration[
                "RocketMQ:Security:Authorization:Clients:test-client:Permissions:0"] =
                permission;
        }

        var publisher = new Mock<IMessagePublisher>();
        publisher
            .Setup(value => value.PublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<Envelope>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                Guid publishId,
                Envelope envelope,
                CancellationToken cancellationToken) =>
                new PublishResult(
                    publishId,
                    Guid.NewGuid(),
                    PublishStatus.Accepted,
                    ["test-queue"]));
        var queueStore = new Mock<IMessageQueueStore>();
        queueStore
            .Setup(value => value.LeaseNextAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeasedMessage?)null);
        queueStore
            .Setup(value => value.AckAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        queueStore
            .Setup(value => value.NackAsync(
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var routingStore = new Mock<IRoutingStore>();
        routingStore
            .Setup(value => value.DeclareExchangeAsync(
                It.IsAny<Exchange>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        routingStore
            .Setup(value => value.DeclareQueueAsync(
                It.IsAny<QueueDefinition>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        routingStore
            .Setup(value => value.BindAsync(
                It.IsAny<Binding>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var server = CreateServer(
            configuration,
            publisher.Object,
            queueStore.Object,
            routingStore.Object);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await server.StartAsync(timeout.Token);
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (
                    _,
                    certificate,
                    _,
                    _) => certificate?.GetCertHashString() == serverCertificate.GetCertHashString()
            };
            handler.ClientCertificates.Add(presentedCertificate);
            using var channel = GrpcChannel.ForAddress(
                $"https://127.0.0.1:{port}",
                new GrpcChannelOptions { HttpHandler = handler });

            if (expectedStatus is null)
            {
                await InvokeRpcAsync(channel.CreateCallInvoker(), rpc, timeout.Token);
            }
            else
            {
                var exception = await Assert.ThrowsAsync<RpcException>(
                    () => InvokeRpcAsync(channel.CreateCallInvoker(), rpc, timeout.Token));
                Assert.Equal(expectedStatus, exception.StatusCode);
                VerifyRpcWasNotDispatched(
                    rpc,
                    publisher,
                    queueStore,
                    routingStore);
            }
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
            Directory.Delete(tempDirectory, true);
        }
    }

    private static void VerifyRpcWasNotDispatched(
        string rpc,
        Mock<IMessagePublisher> publisher,
        Mock<IMessageQueueStore> queueStore,
        Mock<IRoutingStore> routingStore)
    {
        switch (rpc)
        {
            case "Publish":
                publisher.Verify(
                    value => value.PublishAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Envelope>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                break;
            case "LeaseNext":
            case "Ack":
            case "Nack":
                queueStore.VerifyNoOtherCalls();
                break;
            case "DeclareExchange":
            case "DeclareQueue":
            case "Bind":
                routingStore.VerifyNoOtherCalls();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(rpc));
        }
    }

    private static async Task InvokeRpcAsync(
        CallInvoker invoker,
        string rpc,
        CancellationToken cancellationToken)
    {
        switch (rpc)
        {
            case "Publish":
                await InvokeUnaryAsync(
                    invoker,
                    "rocketmq.v1.Producer",
                    "Publish",
                    new PublishRequest
                    {
                        ExchangeName = "test-exchange",
                        PublishId = Guid.NewGuid().ToString()
                    },
                    PublishResponse.Parser,
                    cancellationToken);
                break;
            case "LeaseNext":
                await InvokeUnaryAsync(
                    invoker,
                    "rocketmq.v1.Consumer",
                    "LeaseNext",
                    new LeaseRequest
                    {
                        QueueName = "test-queue",
                        VisibilityTimeoutSeconds = 30
                    },
                    LeaseResponse.Parser,
                    cancellationToken);
                break;
            case "Ack":
                await InvokeUnaryAsync(
                    invoker,
                    "rocketmq.v1.Consumer",
                    "Ack",
                    new AckRequest { LeaseId = Guid.NewGuid().ToString() },
                    AckResponse.Parser,
                    cancellationToken);
                break;
            case "Nack":
                await InvokeUnaryAsync(
                    invoker,
                    "rocketmq.v1.Consumer",
                    "Nack",
                    new NackRequest { LeaseId = Guid.NewGuid().ToString(), Requeue = true },
                    AckResponse.Parser,
                    cancellationToken);
                break;
            case "DeclareExchange":
                await InvokeUnaryAsync(
                    invoker,
                    "rocketmq.v1.Admin",
                    "DeclareExchange",
                    new DeclareExchangeRequest
                    {
                        ExchangeName = "test-exchange",
                        ExchangeType = "Direct"
                    },
                    AdminResponse.Parser,
                    cancellationToken);
                break;
            case "DeclareQueue":
                await InvokeUnaryAsync(
                    invoker,
                    "rocketmq.v1.Admin",
                    "DeclareQueue",
                    new DeclareQueueRequest { QueueName = "test-queue" },
                    AdminResponse.Parser,
                    cancellationToken);
                break;
            case "Bind":
                await InvokeUnaryAsync(
                    invoker,
                    "rocketmq.v1.Admin",
                    "Bind",
                    new BindRequest
                    {
                        ExchangeName = "test-exchange",
                        QueueName = "test-queue",
                        RoutingKey = "test"
                    },
                    AdminResponse.Parser,
                    cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(rpc));
        }
    }

    private static async Task<TResponse> InvokeUnaryAsync<TRequest, TResponse>(
        CallInvoker invoker,
        string serviceName,
        string methodName,
        TRequest request,
        MessageParser<TResponse> responseParser,
        CancellationToken cancellationToken)
        where TRequest : class, IMessage<TRequest>
        where TResponse : class, IMessage<TResponse>
    {
        var method = new Method<TRequest, TResponse>(
            MethodType.Unary,
            serviceName,
            methodName,
            Marshallers.Create<TRequest>(
                message => message.ToByteArray(),
                _ => throw new NotSupportedException()),
            Marshallers.Create<TResponse>(
                message => message.ToByteArray(),
                payload => responseParser.ParseFrom(payload)));
        using var call = invoker.AsyncUnaryCall(
            method,
            host: null,
            new CallOptions(cancellationToken: cancellationToken),
            request);
        return await call.ResponseAsync;
    }

    private static async Task RunMutualTlsHandshakeAsync(
        X509Certificate2 trustedClientCa,
        X509Certificate2? clientCertificate,
        bool shouldSucceed,
        bool usePemCa = false)
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "RocketMQ.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var serverCertificatePath = Path.Combine(tempDirectory, "server.pfx");
        var clientCaPath = Path.Combine(tempDirectory, usePemCa ? "client-ca.pem" : "client-ca.cer");
        const string serverCertificatePassword = "test-password";
        using var serverCertificate = CreateServerCertificate();
        await File.WriteAllBytesAsync(
            serverCertificatePath,
            serverCertificate.Export(X509ContentType.Pfx, serverCertificatePassword),
            TestContext.Current.CancellationToken);
        if (usePemCa)
        {
            await File.WriteAllTextAsync(
                clientCaPath,
                trustedClientCa.ExportCertificatePem(),
                TestContext.Current.CancellationToken);
        }
        else
        {
            await File.WriteAllBytesAsync(
                clientCaPath,
                trustedClientCa.Export(X509ContentType.Cert),
                TestContext.Current.CancellationToken);
        }

        var port = GetAvailablePort();
        var configuration = CreateConfiguration($"https://127.0.0.1:{port}");
        configuration["Kestrel:Endpoints:Grpc:Certificate:Path"] = serverCertificatePath;
        configuration["Kestrel:Endpoints:Grpc:Certificate:Password"] = serverCertificatePassword;
        configuration["RocketMQ:Security:MutualTls:Enabled"] = "true";
        configuration["RocketMQ:Security:MutualTls:TrustedClientCaPath"] = clientCaPath;
        var server = CreateServer(configuration);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await server.StartAsync(timeout.Token);
            var handshake = AuthenticateAsClientAsync(
                port,
                serverCertificate,
                clientCertificate,
                timeout.Token);
            if (shouldSucceed)
            {
                var protocol = await handshake;
                Assert.Equal(SslApplicationProtocol.Http2, protocol);
            }
            else
            {
                await Assert.ThrowsAnyAsync<Exception>(() => handshake);
            }
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
            Directory.Delete(tempDirectory, true);
        }
    }

    private static async Task<SslApplicationProtocol> AuthenticateAsClientAsync(
        int port,
        X509Certificate2 serverCertificate,
        X509Certificate2? clientCertificate,
        CancellationToken cancellationToken)
    {
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
        using var sslStream = new SslStream(
            tcpClient.GetStream(),
            false,
            (_, presentedCertificate, _, _) =>
                presentedCertificate?.GetCertHashString() == serverCertificate.GetCertHashString());
        var options = new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            ApplicationProtocols = [SslApplicationProtocol.Http2],
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        };
        if (clientCertificate is not null)
        {
            options.ClientCertificates = [clientCertificate];
        }

        await sslStream.AuthenticateAsClientAsync(options, cancellationToken);
        await sslStream.WriteAsync(
            "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray(),
            cancellationToken);
        var frameHeader = new byte[9];
        var bytesRead = await sslStream.ReadAsync(frameHeader, cancellationToken);
        if (bytesRead == 0)
        {
            throw new AuthenticationException("The server rejected the TLS connection.");
        }

        return sslStream.NegotiatedApplicationProtocol;
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
