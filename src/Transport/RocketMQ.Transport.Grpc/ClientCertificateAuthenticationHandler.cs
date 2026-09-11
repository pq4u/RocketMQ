using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RocketMQ.Transport.Grpc;

internal sealed class ClientCertificateAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ClientAuthorizationRegistry _registry;

    public ClientCertificateAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ClientAuthorizationRegistry registry)
        : base(options, logger, encoder)
    {
        _registry = registry;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var certificate = await Context.Connection.GetClientCertificateAsync(Context.RequestAborted);
        if (certificate is null)
        {
            Logger.LogWarning(
                "A request to {RpcMethod} did not provide a client certificate.",
                Request.Path);
            return AuthenticateResult.Fail("A client certificate is required.");
        }

        if (!_registry.TryResolve(certificate, out var client, out var fingerprint))
        {
            Logger.LogWarning(
                "Unregistered client certificate {CertificateSha256} attempted {RpcMethod}.",
                fingerprint,
                Request.Path);
            return AuthenticateResult.Fail("The client certificate is not registered.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, client!.ClientId),
            new(ClaimTypes.Name, client.ClientId)
        };
        claims.AddRange(Enum.GetValues<BrokerPermission>()
            .Where(client.HasAnyPermission)
            .Select(permission => new Claim(BrokerClaimTypes.Permission, permission.ToString())));
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
