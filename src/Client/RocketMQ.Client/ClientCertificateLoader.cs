using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RocketMQ.Client;

internal sealed class ClientCertificateHolder : IDisposable
{
    public ClientCertificateHolder(X509Certificate2 certificate) => Certificate = certificate;

    public X509Certificate2 Certificate { get; }

    public void Dispose() => Certificate.Dispose();
}

internal static class ClientCertificateLoader
{
    public static ClientCertificateHolder? Load(RocketMQClientOptions options)
    {
        ValidateEndpoint(options.Endpoint, options.ClientCertificatePath is not null);

        if (string.IsNullOrWhiteSpace(options.ClientCertificatePath))
        {
            if (!string.IsNullOrWhiteSpace(options.ClientCertificateKeyPath)
                || options.ClientCertificatePassword is not null)
            {
                throw new ArgumentException(
                    "ClientCertificatePath is required when a client certificate key or password is configured.");
            }

            return null;
        }

        var certificatePath = ValidateFilePath(
            options.ClientCertificatePath,
            nameof(options.ClientCertificatePath));
        var keyPath = string.IsNullOrWhiteSpace(options.ClientCertificateKeyPath)
            ? null
            : ValidateFilePath(options.ClientCertificateKeyPath, nameof(options.ClientCertificateKeyPath));

        try
        {
            var certificate = keyPath is null
                ? X509CertificateLoader.LoadPkcs12FromFile(
                    certificatePath,
                    options.ClientCertificatePassword,
                    X509KeyStorageFlags.DefaultKeySet)
                : string.IsNullOrEmpty(options.ClientCertificatePassword)
                    ? X509Certificate2.CreateFromPemFile(certificatePath, keyPath)
                    : X509Certificate2.CreateFromEncryptedPemFile(
                        certificatePath,
                        options.ClientCertificatePassword,
                        keyPath);

            if (!certificate.HasPrivateKey)
            {
                certificate.Dispose();
                throw new ArgumentException("The client certificate must include a private key.");
            }

            return new ClientCertificateHolder(certificate);
        }
        catch (CryptographicException exception)
        {
            throw new ArgumentException(
                "The client certificate could not be loaded. Check its format, private key, and password.",
                exception);
        }
    }

    private static void ValidateEndpoint(string endpoint, bool hasClientCertificate)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Endpoint must be an absolute HTTP(S) URI.");
        }

        if (hasClientCertificate && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("A client certificate can be used only with an HTTPS endpoint.");
        }
    }

    private static string ValidateFilePath(string path, string optionName)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"{optionName} must be an absolute path.");
        }

        if (!File.Exists(path))
        {
            throw new ArgumentException($"{optionName} file '{path}' does not exist.");
        }

        return path;
    }
}
