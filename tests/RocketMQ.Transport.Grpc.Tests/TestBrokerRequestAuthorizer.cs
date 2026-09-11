using Grpc.Core;

namespace RocketMQ.Transport.Grpc.Tests;

internal sealed class TestBrokerRequestAuthorizer : IBrokerRequestAuthorizer
{
    public string? LeaseOwnerId { get; init; }

    public bool ResourceNameRequired { get; init; }

    public Action<BrokerPermission, BrokerResourceKind, string>? OnDemand { get; init; }

    public List<(BrokerPermission Permission, BrokerResourceKind Kind, string Name)> Demands { get; } = [];

    public string? GetLeaseOwnerId(ServerCallContext context) => LeaseOwnerId;

    public bool RequiresResourceName(ServerCallContext context, BrokerPermission permission)
        => ResourceNameRequired;

    public void Demand(
        ServerCallContext context,
        BrokerPermission permission,
        BrokerResourceKind resourceKind,
        string resourceName)
    {
        Demands.Add((permission, resourceKind, resourceName));
        OnDemand?.Invoke(permission, resourceKind, resourceName);
    }
}
