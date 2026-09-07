using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace RocketMQ.Transport.Grpc;

internal sealed class ClientCertificateValidator : IDisposable
{
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";
    private readonly X509Certificate2Collection _trustedRoots;
    private readonly X509Certificate2Collection _intermediates;
    private readonly X509RevocationMode _revocationMode;

    private ClientCertificateValidator(
        X509Certificate2Collection trustedRoots,
        X509Certificate2Collection intermediates,
        X509RevocationMode revocationMode)
    {
        _trustedRoots = trustedRoots;
        _intermediates = intermediates;
        _revocationMode = revocationMode;
    }

    public static ClientCertificateValidator Load(IConfiguration configuration)
    {
        var path = configuration["RocketMQ:Security:MutualTls:TrustedClientCaPath"];
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "Configure RocketMQ:Security:MutualTls:TrustedClientCaPath when mutual TLS is enabled.");
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException(
                "RocketMQ:Security:MutualTls:TrustedClientCaPath must be an absolute path.");
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Trusted client CA file '{path}' does not exist.");
        }

        var certificates = LoadCertificates(path);
        var trustedRoots = new X509Certificate2Collection();
        var intermediates = new X509Certificate2Collection();
        try
        {
            foreach (var certificate in certificates)
            {
                var basicConstraints = certificate.Extensions
                    .OfType<X509BasicConstraintsExtension>()
                    .FirstOrDefault();
                if (basicConstraints?.CertificateAuthority != true)
                {
                    throw new InvalidOperationException(
                        $"Certificate '{certificate.Subject}' in the trusted client CA file is not a CA certificate.");
                }

                if (certificate.SubjectName.RawData.SequenceEqual(certificate.IssuerName.RawData))
                {
                    trustedRoots.Add(certificate);
                }
                else
                {
                    intermediates.Add(certificate);
                }
            }

            if (trustedRoots.Count == 0)
            {
                throw new InvalidOperationException(
                    "The trusted client CA file must contain at least one self-signed root CA certificate.");
            }

            return new ClientCertificateValidator(
                trustedRoots,
                intermediates,
                ParseRevocationMode(configuration));
        }
        catch
        {
            DisposeCertificates(certificates);
            throw;
        }
    }

    public bool Validate(X509Certificate2 certificate, X509Chain? _, SslPolicyErrors __)
    {
        var basicConstraints = certificate.Extensions
            .OfType<X509BasicConstraintsExtension>()
            .FirstOrDefault();
        if (basicConstraints?.CertificateAuthority == true)
        {
            return false;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(_trustedRoots);
        chain.ChainPolicy.ExtraStore.AddRange(_intermediates);
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(ClientAuthenticationOid));
        chain.ChainPolicy.RevocationMode = _revocationMode;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        chain.ChainPolicy.DisableCertificateDownloads = _revocationMode == X509RevocationMode.NoCheck;
        return chain.Build(certificate);
    }

    public void Dispose()
    {
        DisposeCertificates(_trustedRoots);
        DisposeCertificates(_intermediates);
    }

    private static X509Certificate2Collection LoadCertificates(string path)
    {
        var certificates = new X509Certificate2Collection();
        try
        {
            var contents = File.ReadAllText(path);
            if (contents.Contains("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal))
            {
                certificates.ImportFromPem(contents);
            }
            else
            {
                certificates.Add(X509CertificateLoader.LoadCertificateFromFile(path));
            }
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException)
        {
            DisposeCertificates(certificates);
            throw new InvalidOperationException(
                $"Trusted client CA file '{path}' could not be loaded as PEM or DER.",
                exception);
        }

        if (certificates.Count == 0)
        {
            throw new InvalidOperationException(
                $"Trusted client CA file '{path}' does not contain a certificate.");
        }

        return certificates;
    }

    private static X509RevocationMode ParseRevocationMode(IConfiguration configuration)
    {
        var value = configuration["RocketMQ:Security:MutualTls:RevocationMode"] ?? "NoCheck";
        if (!Enum.TryParse<X509RevocationMode>(value, true, out var mode)
            || mode is not (X509RevocationMode.NoCheck or X509RevocationMode.Offline or X509RevocationMode.Online))
        {
            throw new InvalidOperationException(
                "RocketMQ:Security:MutualTls:RevocationMode must be NoCheck, Offline, or Online.");
        }

        return mode;
    }

    private static void DisposeCertificates(IEnumerable<X509Certificate2> certificates)
    {
        foreach (var certificate in certificates)
        {
            certificate.Dispose();
        }
    }
}
