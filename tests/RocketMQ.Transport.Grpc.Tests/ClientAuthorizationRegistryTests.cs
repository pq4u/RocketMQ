using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace RocketMQ.Transport.Grpc.Tests;

public sealed class ClientAuthorizationRegistryTests
{
    private const string FingerprintA =
        "00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF";
    private const string FingerprintB =
        "FFEEDDCCBBAA99887766554433221100FFEEDDCCBBAA99887766554433221100";

    [Fact]
    public void IsEnabled_WithInvalidValue_RejectsConfiguration()
    {
        var configuration = new ConfigurationManager();
        configuration["RocketMQ:Security:Authorization:Enabled"] = "sometimes";

        var exception = Assert.Throws<InvalidOperationException>(
            () => ClientAuthorizationRegistry.IsEnabled(configuration));

        Assert.Contains("must be true or false", exception.Message);
    }

    [Fact]
    public void Load_WithMultipleFingerprintsAndPermissions_AcceptsConfiguration()
    {
        var configuration = CreateClientConfiguration(
            ["00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF", FingerprintB],
            ["publish", "Consume"]);

        var registry = ClientAuthorizationRegistry.Load(configuration);

        Assert.NotNull(registry);
    }

    [Fact]
    public void Load_WithoutClients_RejectsConfiguration()
    {
        var configuration = new ConfigurationManager();

        var exception = Assert.Throws<InvalidOperationException>(
            () => ClientAuthorizationRegistry.Load(configuration));

        Assert.Contains("at least one", exception.Message);
    }

    [Theory]
    [InlineData("1234", "Publish", "invalid SHA-256")]
    [InlineData("", "Publish", "at least one CertificateSha256Fingerprints")]
    [InlineData(FingerprintA, "Delete", "unknown permission")]
    [InlineData(FingerprintA, "", "at least one Permissions")]
    public void Load_WithInvalidClientEntry_RejectsConfiguration(
        string fingerprint,
        string permission,
        string expectedMessage)
    {
        var configuration = CreateClientConfiguration(
            [fingerprint],
            string.IsNullOrEmpty(permission) ? [] : [permission]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => ClientAuthorizationRegistry.Load(configuration));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public void Load_WithDuplicateFingerprintAcrossClients_RejectsConfiguration()
    {
        var configuration = CreateClientConfiguration([FingerprintA], ["Publish"]);
        configuration[
            "RocketMQ:Security:Authorization:Clients:second:CertificateSha256Fingerprints:0"] =
            FingerprintA.ToLowerInvariant();
        configuration["RocketMQ:Security:Authorization:Clients:second:Permissions:0"] = "Consume";

        var exception = Assert.Throws<InvalidOperationException>(
            () => ClientAuthorizationRegistry.Load(configuration));

        Assert.Contains("configured more than once", exception.Message);
    }

    [Fact]
    public void Load_WithDuplicatePermission_RejectsConfiguration()
    {
        var configuration = CreateClientConfiguration(
            [FingerprintA],
            ["Publish", "publish"]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => ClientAuthorizationRegistry.Load(configuration));

        Assert.Contains("permission 'Publish' more than once", exception.Message);
    }

    [Fact]
    public void TryResolve_WithSecondFingerprint_MapsToSameClientDuringRotation()
    {
        using var firstCertificate = CreateCertificate("CN=first");
        using var secondCertificate = CreateCertificate("CN=second");
        var configuration = CreateClientConfiguration(
            [
                firstCertificate.GetCertHashString(HashAlgorithmName.SHA256),
                secondCertificate.GetCertHashString(HashAlgorithmName.SHA256)
            ],
            ["Publish"]);
        var registry = ClientAuthorizationRegistry.Load(configuration);

        var resolved = registry.TryResolve(
            secondCertificate,
            out var client,
            out var fingerprint);

        Assert.True(resolved);
        Assert.NotNull(client);
        Assert.Equal("test-client", client.ClientId);
        Assert.Contains(BrokerPermission.Publish, client.Permissions);
        Assert.Equal(
            secondCertificate.GetCertHashString(HashAlgorithmName.SHA256),
            fingerprint);
    }

    private static ConfigurationManager CreateClientConfiguration(
        IReadOnlyList<string> fingerprints,
        IReadOnlyList<string> permissions)
    {
        var configuration = new ConfigurationManager();
        for (var index = 0; index < fingerprints.Count; index++)
        {
            configuration[
                $"RocketMQ:Security:Authorization:Clients:test-client:CertificateSha256Fingerprints:{index}"] =
                fingerprints[index];
        }

        for (var index = 0; index < permissions.Count; index++)
        {
            configuration[
                $"RocketMQ:Security:Authorization:Clients:test-client:Permissions:{index}"] =
                permissions[index];
        }

        return configuration;
    }

    private static X509Certificate2 CreateCertificate(string subject)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            subject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(
            generated.Export(X509ContentType.Pfx),
            null,
            X509KeyStorageFlags.EphemeralKeySet);
    }
}
