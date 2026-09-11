using System.Security.Claims;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace RocketMQ.Transport.Grpc;

internal interface IBrokerRequestAuthorizer
{
    string? GetLeaseOwnerId(ServerCallContext context);

    bool RequiresResourceName(ServerCallContext context, BrokerPermission permission);

    void Demand(
        ServerCallContext context,
        BrokerPermission permission,
        BrokerResourceKind resourceKind,
        string resourceName);
}

internal sealed class DisabledBrokerRequestAuthorizer : IBrokerRequestAuthorizer
{
    public static DisabledBrokerRequestAuthorizer Instance { get; } = new();

    private DisabledBrokerRequestAuthorizer() { }

    public string? GetLeaseOwnerId(ServerCallContext context) => null;

    public bool RequiresResourceName(ServerCallContext context, BrokerPermission permission) => false;

    public void Demand(
        ServerCallContext context,
        BrokerPermission permission,
        BrokerResourceKind resourceKind,
        string resourceName)
    {
    }
}

internal sealed class BrokerRequestAuthorizer : IBrokerRequestAuthorizer
{
    private readonly ClientAuthorizationRegistry _registry;
    private readonly ILogger<BrokerRequestAuthorizer> _logger;

    public BrokerRequestAuthorizer(
        ClientAuthorizationRegistry registry,
        ILogger<BrokerRequestAuthorizer> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public string? GetLeaseOwnerId(ServerCallContext context) => ResolveClient(context).ClientId;

    public bool RequiresResourceName(ServerCallContext context, BrokerPermission permission)
        => !ResolveClient(context).Permissions.Contains(permission);

    public void Demand(
        ServerCallContext context,
        BrokerPermission permission,
        BrokerResourceKind resourceKind,
        string resourceName)
    {
        var client = ResolveClient(context);
        if (client.CanAccess(permission, resourceKind, resourceName))
        {
            return;
        }

        _logger.LogWarning(
            "Client {ClientId} was denied {Permission} access to {ResourceKind} {ResourceName} on {RpcMethod}.",
            client.ClientId,
            permission,
            resourceKind,
            resourceName,
            context.Method);
        throw new RpcException(new Status(
            StatusCode.PermissionDenied,
            "Access to the requested resource is denied."));
    }

    private AuthorizedClient ResolveClient(ServerCallContext context)
    {
        var clientId = context.GetHttpContext().User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (clientId is null || !_registry.TryResolve(clientId, out var client))
        {
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "The authenticated client is not registered."));
        }

        return client!;
    }
}
