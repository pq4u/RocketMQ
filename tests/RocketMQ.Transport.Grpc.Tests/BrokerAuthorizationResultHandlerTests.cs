using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace RocketMQ.Transport.Grpc.Tests;

public sealed class BrokerAuthorizationResultHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenForbidden_LogsClientPermissionAndRpcWithoutPayload()
    {
        var logger = new CapturingLogger<BrokerAuthorizationResultHandler>();
        var handler = new BrokerAuthorizationResultHandler(logger);
        var context = new DefaultHttpContext();
        var authenticationService = new Mock<IAuthenticationService>();
        authenticationService
            .Setup(value => value.ForbidAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string?>(),
                It.IsAny<AuthenticationProperties?>()))
            .Returns(Task.CompletedTask);
        using var serviceProvider = new ServiceCollection()
            .AddSingleton(authenticationService.Object)
            .BuildServiceProvider();
        context.RequestServices = serviceProvider;
        context.Request.Path = "/rocketmq.v1.Producer/Publish";
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "orders-service")],
                BrokerAuthenticationDefaults.Scheme));
        var policy = new AuthorizationPolicy(
            [new BrokerPermissionRequirement(BrokerPermission.Publish)],
            [BrokerAuthenticationDefaults.Scheme]);

        await handler.HandleAsync(
            _ => Task.CompletedTask,
            context,
            policy,
            PolicyAuthorizationResult.Forbid());

        var message = Assert.Single(logger.Messages);
        Assert.Contains("orders-service", message);
        Assert.Contains("Publish", message);
        Assert.Contains("/rocketmq.v1.Producer/Publish", message);
        Assert.DoesNotContain("payload", message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Messages.Add(formatter(state, exception));
            }
        }
    }
}
