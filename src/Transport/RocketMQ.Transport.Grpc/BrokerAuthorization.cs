using Microsoft.AspNetCore.Authorization;

namespace RocketMQ.Transport.Grpc;

internal enum BrokerPermission
{
    Publish,
    Consume,
    Admin
}

internal static class BrokerAuthenticationDefaults
{
    public const string Scheme = "RocketMQ.ClientCertificate";
}

internal static class BrokerAuthorizationPolicies
{
    public const string Publish = "RocketMQ.Publish";
    public const string Consume = "RocketMQ.Consume";
    public const string Admin = "RocketMQ.Admin";
}

internal static class BrokerClaimTypes
{
    public const string Permission = "rocketmq.permission";
}

internal sealed record BrokerPermissionRequirement(BrokerPermission Permission) : IAuthorizationRequirement;

internal sealed class BrokerPermissionAuthorizationHandler
    : AuthorizationHandler<BrokerPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BrokerPermissionRequirement requirement)
    {
        if (context.User.HasClaim(
                BrokerClaimTypes.Permission,
                requirement.Permission.ToString()))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
