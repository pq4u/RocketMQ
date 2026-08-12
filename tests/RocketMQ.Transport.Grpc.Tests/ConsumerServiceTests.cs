using Grpc.Core;
using Moq;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Transport.Grpc.Protos;
using RocketMQ.Transport.Grpc.Services;
using Xunit;

namespace RocketMQ.Transport.Grpc.Tests;

public class ConsumerServiceTests
{
    private readonly Mock<IMessageQueueStore> _queueStoreMock;
    private readonly ConsumerService _service;
    private readonly TestServerCallContext _context;

    public ConsumerServiceTests()
    {
        _queueStoreMock = new Mock<IMessageQueueStore>();
        _service = new ConsumerService(_queueStoreMock.Object);
        _context = new TestServerCallContext();
    }

    [Fact]
    public async Task LeaseNext_MessageAvailable_ReturnsLeasedMessage()
    {
        // Arrange
        var request = new LeaseRequest { QueueName = "my-queue", VisibilityTimeoutSeconds = 30 };
        var messageId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var inboundMessage = new InboundMessage(Guid.NewGuid(), messageId, new byte[] { 1, 2, 3 }, DateTimeOffset.UtcNow);
        var storeMessageId = Guid.NewGuid();
        var leasedMessage = new LeasedMessage(storeMessageId, leaseId, inboundMessage, 1, DateTimeOffset.UtcNow.AddSeconds(30));

        _queueStoreMock.Setup(x => x.LeaseNextAsync("my-queue", TimeSpan.FromSeconds(30), It.IsAny<CancellationToken>()))
            .ReturnsAsync(leasedMessage);

        // Act
        var response = await _service.LeaseNext(request, _context);

        // Assert
        Assert.Equal(leaseId.ToString(), response.LeaseId);
        Assert.Equal(storeMessageId.ToString(), response.MessageId);
        Assert.Equal(messageId.ToString(), response.CorrelationId);
        Assert.Equal(1, response.DeliveryCount);
        Assert.Equal(new byte[] { 1, 2, 3 }, response.Payload.ToByteArray());
    }

    [Fact]
    public async Task LeaseNext_NoMessageAvailable_ReturnsEmptyResponse()
    {
        // Arrange
        var request = new LeaseRequest { QueueName = "my-queue", VisibilityTimeoutSeconds = 30 };
        _queueStoreMock.Setup(x => x.LeaseNextAsync("my-queue", TimeSpan.FromSeconds(30), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeasedMessage?)null);

        // Act
        var response = await _service.LeaseNext(request, _context);

        // Assert
        Assert.Empty(response.LeaseId);
        Assert.Empty(response.Payload);
    }

    [Fact]
    public async Task Ack_ValidRequest_CallsQueueStore()
    {
        // Arrange
        var leaseId = Guid.NewGuid();
        var request = new AckRequest { LeaseId = leaseId.ToString() };

        _queueStoreMock.Setup(x => x.AckAsync(leaseId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _service.Ack(request, _context);

        // Assert
        Assert.NotNull(response);
        _queueStoreMock.Verify(x => x.AckAsync(leaseId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Nack_ValidRequest_CallsQueueStore()
    {
        // Arrange
        var leaseId = Guid.NewGuid();
        var request = new NackRequest { LeaseId = leaseId.ToString(), Requeue = true };

        _queueStoreMock.Setup(x => x.NackAsync(leaseId, true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _service.Nack(request, _context);

        // Assert
        Assert.NotNull(response);
        _queueStoreMock.Verify(x => x.NackAsync(leaseId, true, It.IsAny<CancellationToken>()), Times.Once);
    }
}
