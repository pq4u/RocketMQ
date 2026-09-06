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

    private static GrpcTransportServer CreateServer(IConfiguration configuration)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(Environments.Development);
        environment.SetupGet(value => value.ContentRootPath).Returns(Directory.GetCurrentDirectory());
        return new GrpcTransportServer(
            Mock.Of<IMessagePublisher>(),
            Mock.Of<IMessageQueueStore>(),
            Mock.Of<IRoutingStore>(),
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
