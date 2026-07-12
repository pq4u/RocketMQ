using Grpc.Core;
using Moq;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Transport.Grpc.Protos;
using RocketMQ.Transport.Grpc.Services;
using Xunit;

namespace RocketMQ.Transport.Grpc.Tests;

public class AdminServiceTests
{
    private readonly Mock<IRoutingStore> _routingStoreMock;
    private readonly AdminService _service;
    private readonly TestServerCallContext _context;

    public AdminServiceTests()
    {
        _routingStoreMock = new Mock<IRoutingStore>();
        _service = new AdminService(_routingStoreMock.Object);
        _context = new TestServerCallContext();
    }

    [Fact]
    public async Task DeclareExchange_ValidRequest_CallsRoutingStore()
    {
        // Arrange
        var request = new DeclareExchangeRequest { ExchangeName = "my-exchange", ExchangeType = "Fanout" };
        _routingStoreMock.Setup(x => x.DeclareExchangeAsync(It.IsAny<Exchange>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _service.DeclareExchange(request, _context);

        // Assert
        Assert.True(response.Success);
        _routingStoreMock.Verify(x => x.DeclareExchangeAsync(It.Is<Exchange>(e => 
            e.Name == "my-exchange" && e.Type == ExchangeType.Fanout && e.Durable == true), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeclareQueue_ValidRequest_CallsRoutingStore()
    {
        // Arrange
        var request = new DeclareQueueRequest { QueueName = "my-queue" };
        _routingStoreMock.Setup(x => x.DeclareQueueAsync(It.IsAny<QueueDefinition>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _service.DeclareQueue(request, _context);

        // Assert
        Assert.True(response.Success);
        _routingStoreMock.Verify(x => x.DeclareQueueAsync(It.Is<QueueDefinition>(q => 
            q.Name == "my-queue" && q.Durable == true), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Bind_ValidRequest_CallsRoutingStore()
    {
        // Arrange
        var request = new BindRequest { ExchangeName = "my-exchange", QueueName = "my-queue", RoutingKey = "my.routing.key" };
        _routingStoreMock.Setup(x => x.BindAsync(It.IsAny<Binding>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _service.Bind(request, _context);

        // Assert
        Assert.True(response.Success);
        _routingStoreMock.Verify(x => x.BindAsync(It.Is<Binding>(b => 
            b.ExchangeName == "my-exchange" && b.QueueName == "my-queue" && b.RoutingKey == "my.routing.key"), 
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
