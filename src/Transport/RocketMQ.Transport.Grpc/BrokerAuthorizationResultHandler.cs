using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace RocketMQ.Transport.Grpc;

internal sealed class BrokerAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    private readonly ILogger<BrokerAuthorizationResultHandler> _logger;

    public BrokerAuthorizationResultHandler(ILogger<BrokerAuthorizationResultHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            var clientId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
            var permissions = policy.Requirements
                .OfType<BrokerPermissionRequirement>()
                .Select(requirement => requirement.Permission.ToString())
                .ToArray();
            _logger.LogWarning(
                "Client {ClientId} was denied {Permissions} access to {RpcMethod}.",
                clientId,
                string.Join(',', permissions),
                context.Request.Path);
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
