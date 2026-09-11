using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace RocketMQ.Transport.Grpc;

internal sealed record AuthorizedClient(
    string ClientId,
    IReadOnlySet<BrokerPermission> Permissions,
    BrokerResourcePermissions Resources)
{
    public bool HasAnyPermission(BrokerPermission permission)
        => Permissions.Contains(permission) || Resources.HasAny(permission);

    public bool CanAccess(
        BrokerPermission permission,
        BrokerResourceKind resourceKind,
        string resourceName)
        => Permissions.Contains(permission) || Resources.CanAccess(permission, resourceKind, resourceName);
}

internal sealed record BrokerResourcePermissions(
    IReadOnlySet<string> PublishExchanges,
    IReadOnlySet<string> AdminExchanges,
    IReadOnlySet<string> ConsumeQueues,
    IReadOnlySet<string> AdminQueues)
{
    public bool HasAny(BrokerPermission permission) => permission switch
    {
        BrokerPermission.Publish => PublishExchanges.Count > 0,
        BrokerPermission.Consume => ConsumeQueues.Count > 0,
        BrokerPermission.Admin => AdminExchanges.Count > 0 || AdminQueues.Count > 0,
        _ => false
    };

    public bool CanAccess(
        BrokerPermission permission,
        BrokerResourceKind resourceKind,
        string resourceName)
        => (permission, resourceKind) switch
        {
            (BrokerPermission.Publish, BrokerResourceKind.Exchange) => PublishExchanges.Contains(resourceName),
            (BrokerPermission.Admin, BrokerResourceKind.Exchange) => AdminExchanges.Contains(resourceName),
            (BrokerPermission.Consume, BrokerResourceKind.Queue) => ConsumeQueues.Contains(resourceName),
            (BrokerPermission.Admin, BrokerResourceKind.Queue) => AdminQueues.Contains(resourceName),
            _ => false
        };
}

internal sealed class ClientAuthorizationRegistry
{
    private const string SectionPath = "RocketMQ:Security:Authorization";
    private readonly IReadOnlyDictionary<string, AuthorizedClient> _clientsByFingerprint;
    private readonly IReadOnlyDictionary<string, AuthorizedClient> _clientsById;

    private ClientAuthorizationRegistry(
        IReadOnlyDictionary<string, AuthorizedClient> clientsByFingerprint,
        IReadOnlyDictionary<string, AuthorizedClient> clientsById)
    {
        _clientsByFingerprint = clientsByFingerprint;
        _clientsById = clientsById;
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
        var clientsById = new Dictionary<string, AuthorizedClient>(StringComparer.Ordinal);
        foreach (var clientSection in clientSections)
        {
            var clientId = clientSection.Key.Trim();
            if (clientId.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Every {SectionPath}:Clients entry must have a non-empty ClientId.");
            }

            var permissions = ParsePermissions(clientSection, clientId);
            var resources = ParseResourcePermissions(clientSection, clientId);
            if (permissions.Count == 0
                && !Enum.GetValues<BrokerPermission>().Any(resources.HasAny))
            {
                throw new InvalidOperationException(
                    $"Client '{clientId}' must configure at least one global or resource permission.");
            }

            var authorizedClient = new AuthorizedClient(clientId, permissions, resources);
            clientsById.Add(clientId, authorizedClient);
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

        return new ClientAuthorizationRegistry(clientsByFingerprint, clientsById);
    }

    public bool TryResolve(
        X509Certificate2 certificate,
        out AuthorizedClient? authorizedClient,
        out string fingerprint)
    {
        fingerprint = CalculateFingerprint(certificate);
        return _clientsByFingerprint.TryGetValue(fingerprint, out authorizedClient);
    }

    public bool TryResolve(
        string clientId,
        out AuthorizedClient? authorizedClient)
        => _clientsById.TryGetValue(clientId, out authorizedClient);

    private static HashSet<BrokerPermission> ParsePermissions(
        IConfigurationSection clientSection,
        string clientId)
    {
        var values = ReadValues(clientSection.GetSection("Permissions"));
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

    private static BrokerResourcePermissions ParseResourcePermissions(
        IConfigurationSection clientSection,
        string clientId)
    {
        var resourcesSection = clientSection.GetSection("Resources");
        ValidateKeys(
            resourcesSection,
            clientId,
            "Resources",
            new HashSet<string>(["Exchanges", "Queues"], StringComparer.OrdinalIgnoreCase));
        var exchangesSection = resourcesSection.GetSection("Exchanges");
        var queuesSection = resourcesSection.GetSection("Queues");
        ValidateKeys(
            exchangesSection,
            clientId,
            "Resources:Exchanges",
            new HashSet<string>(["Publish", "Admin"], StringComparer.OrdinalIgnoreCase));
        ValidateKeys(
            queuesSection,
            clientId,
            "Resources:Queues",
            new HashSet<string>(["Consume", "Admin"], StringComparer.OrdinalIgnoreCase));

        return new BrokerResourcePermissions(
            ReadResourceNames(exchangesSection.GetSection("Publish"), clientId, "Resources:Exchanges:Publish"),
            ReadResourceNames(exchangesSection.GetSection("Admin"), clientId, "Resources:Exchanges:Admin"),
            ReadResourceNames(queuesSection.GetSection("Consume"), clientId, "Resources:Queues:Consume"),
            ReadResourceNames(queuesSection.GetSection("Admin"), clientId, "Resources:Queues:Admin"));
    }

    private static void ValidateKeys(
        IConfigurationSection section,
        string clientId,
        string relativePath,
        IReadOnlySet<string> allowedKeys)
    {
        foreach (var child in section.GetChildren())
        {
            if (!allowedKeys.Contains(child.Key))
            {
                throw new InvalidOperationException(
                    $"Client '{clientId}' has unknown authorization section '{relativePath}:{child.Key}'.");
            }
        }
    }

    private static HashSet<string> ReadResourceNames(
        IConfigurationSection section,
        string clientId,
        string relativePath)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in section.GetChildren())
        {
            var value = child.Value;
            if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Client '{clientId}' has an empty or whitespace-padded resource name in '{relativePath}'.");
            }

            if (!names.Add(value))
            {
                throw new InvalidOperationException(
                    $"Client '{clientId}' configures resource '{value}' more than once in '{relativePath}'.");
            }
        }

        return names;
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
