using Grpc.Core;
using Moq;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Transport.Grpc.Protos;
using RocketMQ.Transport.Grpc.Services;
using Xunit;

namespace RocketMQ.Transport.Grpc.Tests;

public sealed class ProducerServiceTests
{
    private readonly Mock<IMessagePublisher> _publisher = new();
    private readonly TestServerCallContext _context = new();

    [Fact]
    public async Task Publish_ValidRequest_ReturnsDurableRoutingOutcome()
    {
        var publishId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        _publisher.Setup(x => x.PublishAsync(publishId, It.IsAny<Envelope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishResult(publishId, messageId, PublishStatus.Accepted, ["orders"]));
        var service = new ProducerService(_publisher.Object);

        var response = await service.Publish(new PublishRequest
        {
            ExchangeName = "orders",
            RoutingKey = "created",
            CorrelationId = Guid.NewGuid().ToString(),
            PublishId = publishId.ToString(),
            Payload = Google.Protobuf.ByteString.CopyFromUtf8("hello")
        }, _context);

        Assert.True(response.Success);
        Assert.Equal(messageId.ToString(), response.MessageId);
        Assert.Equal("Accepted", response.Status);
        Assert.Equal(["orders"], response.DestinationQueues);
    }

    [Fact]
    public async Task Publish_UnknownExchange_ReturnsNotFound()
    {
        _publisher.Setup(x => x.PublishAsync(It.IsAny<Guid>(), It.IsAny<Envelope>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Exchange missing"));
        var service = new ProducerService(_publisher.Object);

        var exception = await Assert.ThrowsAsync<RpcException>(() => service.Publish(new PublishRequest { ExchangeName = "missing" }, _context));
        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task Publish_DiagnosticsRequested_ReturnsServerTimings()
    {
        var publishId = Guid.NewGuid();
        _publisher.Setup(x => x.PublishAsync(publishId, It.IsAny<Envelope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishResult(publishId, Guid.NewGuid(), PublishStatus.Accepted, ["orders"]));
        var service = new ProducerService(_publisher.Object);

        var response = await service.Publish(new PublishRequest
        {
            ExchangeName = "orders",
            PublishId = publishId.ToString(),
            IncludeDiagnostics = true
        }, _context);

        Assert.NotNull(response.Diagnostics);
        Assert.True(response.Diagnostics.ServerTotalMs >= 0);
    }

    [Fact]
    public async Task Publish_WhenExchangeAclDenies_DoesNotCallPublisher()
    {
        var authorizer = new TestBrokerRequestAuthorizer
        {
            OnDemand = (_, _, _) => throw new RpcException(new Status(
                StatusCode.PermissionDenied,
                "Access to the requested resource is denied."))
        };
        var service = new ProducerService(_publisher.Object, authorizer);

        var exception = await Assert.ThrowsAsync<RpcException>(() => service.Publish(
            new PublishRequest { ExchangeName = "other-exchange" },
            new TestServerCallContext()));

        Assert.Equal(StatusCode.PermissionDenied, exception.StatusCode);
        _publisher.Verify(x => x.PublishAsync(
            It.IsAny<Guid>(),
            It.IsAny<Envelope>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
