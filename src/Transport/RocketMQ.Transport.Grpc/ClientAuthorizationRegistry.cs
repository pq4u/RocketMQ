using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace RocketMQ.Transport.Grpc;

internal sealed record AuthorizedClient(
    string ClientId,
    IReadOnlySet<BrokerPermission> Permissions);

internal sealed class ClientAuthorizationRegistry
{
    private const string SectionPath = "RocketMQ:Security:Authorization";
    private readonly IReadOnlyDictionary<string, AuthorizedClient> _clientsByFingerprint;

    private ClientAuthorizationRegistry(
        IReadOnlyDictionary<string, AuthorizedClient> clientsByFingerprint)
    {
        _clientsByFingerprint = clientsByFingerprint;
    }

    public static bool IsEnabled(IConfiguration configuration)
    {
        var value = configuration[$"{SectionPath}:Enabled"] ?? "false";
        if (!bool.TryParse(value, out var enabled))
        {
            throw new InvalidOperationException(
                $"{SectionPath}:Enabled must be true or false.");
        }

        return enabled;
    }

    public static ClientAuthorizationRegistry Load(IConfiguration configuration)
    {
        var clientSections = configuration
            .GetSection($"{SectionPath}:Clients")
            .GetChildren()
            .ToArray();
        if (clientSections.Length == 0)
        {
            throw new InvalidOperationException(
                $"Configure at least one {SectionPath}:Clients entry when authorization is enabled.");
        }

        var clientsByFingerprint = new Dictionary<string, AuthorizedClient>(StringComparer.Ordinal);
        foreach (var clientSection in clientSections)
        {
            var clientId = clientSection.Key.Trim();
            if (clientId.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Every {SectionPath}:Clients entry must have a non-empty ClientId.");
            }

            var permissions = ParsePermissions(clientSection, clientId);
            var authorizedClient = new AuthorizedClient(clientId, permissions);
            var fingerprints = ReadValues(
                clientSection.GetSection("CertificateSha256Fingerprints"));
            if (fingerprints.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Client '{clientId}' must configure at least one CertificateSha256Fingerprints value.");
            }

            foreach (var value in fingerprints)
            {
                var fingerprint = NormalizeFingerprint(value, clientId);
                if (!clientsByFingerprint.TryAdd(fingerprint, authorizedClient))
                {
                    throw new InvalidOperationException(
                        $"Certificate SHA-256 fingerprint '{fingerprint}' is configured more than once.");
                }
            }
        }

        return new ClientAuthorizationRegistry(clientsByFingerprint);
    }

    public bool TryResolve(
        X509Certificate2 certificate,
        out AuthorizedClient? authorizedClient,
        out string fingerprint)
    {
        fingerprint = CalculateFingerprint(certificate);
        return _clientsByFingerprint.TryGetValue(fingerprint, out authorizedClient);
    }

    private static HashSet<BrokerPermission> ParsePermissions(
        IConfigurationSection clientSection,
        string clientId)
    {
        var values = ReadValues(clientSection.GetSection("Permissions"));
        if (values.Length == 0)
        {
            throw new InvalidOperationException(
                $"Client '{clientId}' must configure at least one Permissions value.");
        }

        var permissions = new HashSet<BrokerPermission>();
        foreach (var value in values)
        {
            var canonicalName = Enum.GetNames<BrokerPermission>()
                .FirstOrDefault(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase));
            if (canonicalName is null)
            {
                throw new InvalidOperationException(
                    $"Client '{clientId}' has unknown permission '{value}'. " +
                    "Allowed values are Publish, Consume, and Admin.");
            }

            var permission = Enum.Parse<BrokerPermission>(canonicalName);
            if (!permissions.Add(permission))
            {
                throw new InvalidOperationException(
                    $"Client '{clientId}' configures permission '{permission}' more than once.");
            }
        }

        return permissions;
    }

    private static string[] ReadValues(IConfigurationSection section)
        => section
            .GetChildren()
            .Select(child => child.Value?.Trim())
            .Where(value => !string.IsNullOrEmpty(value))
            .Cast<string>()
            .ToArray();

    private static string NormalizeFingerprint(string value, string clientId)
    {
        var normalized = new string(
                value.Where(character => character != ':' && !char.IsWhiteSpace(character)).ToArray())
            .ToUpperInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException(
                $"Client '{clientId}' has an invalid SHA-256 certificate fingerprint '{value}'. " +
                "Use 64 hexadecimal characters; colons are optional.");
        }

        return normalized;
    }

    private static string CalculateFingerprint(X509Certificate2 certificate)
        => Convert.ToHexString(SHA256.HashData(certificate.RawData));
}
