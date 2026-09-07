using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using RocketMQ.Client;

namespace RocketMQ.Client.Tests;

public sealed class RocketMQClientOptionsTests
{
    [Fact]
    public void Endpoint_DefaultsToRunnerHttpsEndpoint()
    {
        var options = new RocketMQClientOptions();

        Assert.Equal("https://localhost:50051", options.Endpoint);
    }

    [Fact]
    public void AddRocketMQClient_WithPfxCertificate_RegistersAllGrpcClients()
    {
        var tempDirectory = CreateTempDirectory();
        var certificatePath = Path.Combine(tempDirectory, "client.pfx");
        const string password = "test-password";
        using var certificate = CreateClientCertificate();
        File.WriteAllBytes(
            certificatePath,
            certificate.Export(X509ContentType.Pfx, password));

        try
        {
            var services = new ServiceCollection();
            services.AddRocketMQClient(options =>
            {
                options.ClientCertificatePath = certificatePath;
                options.ClientCertificatePassword = password;
            });

            using var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<RocketMQ.Transport.Grpc.Protos.Producer.ProducerClient>());
            Assert.NotNull(provider.GetService<RocketMQ.Transport.Grpc.Protos.Consumer.ConsumerClient>());
            Assert.NotNull(provider.GetService<RocketMQ.Transport.Grpc.Protos.Admin.AdminClient>());
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void AddRocketMQClient_WithPemCertificateAndKey_AcceptsConfiguration()
    {
        var tempDirectory = CreateTempDirectory();
        var certificatePath = Path.Combine(tempDirectory, "client.pem");
        var keyPath = Path.Combine(tempDirectory, "client.key");
        using var certificate = CreateClientCertificate();
        using var privateKey = certificate.GetRSAPrivateKey();
        File.WriteAllText(certificatePath, certificate.ExportCertificatePem());
        File.WriteAllText(keyPath, privateKey!.ExportPkcs8PrivateKeyPem());

        try
        {
            var services = new ServiceCollection();

            services.AddRocketMQClient(options =>
            {
                options.ClientCertificatePath = certificatePath;
                options.ClientCertificateKeyPath = keyPath;
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void AddRocketMQClient_WithCertificateAndHttpEndpoint_RejectsConfiguration()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() => services.AddRocketMQClient(options =>
        {
            options.Endpoint = "http://localhost:50051";
            options.ClientCertificatePath = "D:\\certs\\client.pfx";
        }));

        Assert.Contains("only with an HTTPS endpoint", exception.Message);
    }

    [Fact]
    public void AddRocketMQClient_WithKeyButNoCertificate_RejectsConfiguration()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() => services.AddRocketMQClient(options =>
        {
            options.ClientCertificateKeyPath = "D:\\certs\\client.key";
        }));

        Assert.Contains("ClientCertificatePath is required", exception.Message);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "RocketMQ.Client.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static X509Certificate2 CreateClientCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=RocketMQ client",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.2") },
                true));
        using var generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(
            generated.Export(X509ContentType.Pfx),
            null,
            X509KeyStorageFlags.Exportable);
    }
}
