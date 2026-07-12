using Grpc.Core;
using Moq;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Transport.Grpc.Protos;
using RocketMQ.Transport.Grpc.Services;
using Xunit;

namespace RocketMQ.Transport.Grpc.Tests;

public class ProducerServiceTests
{
    private readonly Mock<IMessageChannel<Envelope>> _channelMock;
    private readonly ProducerService _service;
    private readonly TestServerCallContext _context;

    public ProducerServiceTests()
    {
        _channelMock = new Mock<IMessageChannel<Envelope>>();
        _service = new ProducerService(_channelMock.Object);
        _context = new TestServerCallContext();
    }

    [Fact]
    public async Task Publish_ValidRequest_WritesToChannelAndReturnsSuccess()
    {
        // Arrange
        var request = new PublishRequest
        {
            ExchangeName = "my-exchange",
            RoutingKey = "my.routing.key",
            Payload = Google.Protobuf.ByteString.CopyFromUtf8("hello world"),
            CorrelationId = Guid.NewGuid().ToString()
        };

        _channelMock.Setup(x => x.WriteAsync(It.IsAny<Envelope>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        // Act
        var response = await _service.Publish(request, _context);

        // Assert
        Assert.True(response.Success);
        _channelMock.Verify(x => x.WriteAsync(It.Is<Envelope>(e => 
            e.ExchangeName == "my-exchange" &&
            e.RoutingKey == "my.routing.key" &&
            e.Message.CorrelationId == Guid.Parse(request.CorrelationId) &&
            e.Message.Payload.ToArray().SequenceEqual(request.Payload.ToByteArray())
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_ChannelFull_ThrowsResourceExhausted()
    {
        // Arrange
        var request = new PublishRequest
        {
            ExchangeName = "my-exchange",
            RoutingKey = "my.routing.key",
            Payload = Google.Protobuf.ByteString.CopyFromUtf8("hello world")
        };

        _channelMock.Setup(x => x.WriteAsync(It.IsAny<Envelope>(), It.IsAny<CancellationToken>()))
            .Throws(new OperationCanceledException());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<RpcException>(() => _service.Publish(request, _context));
        Assert.Equal(StatusCode.ResourceExhausted, ex.StatusCode);
        Assert.Contains("backpressure applied", ex.Status.Detail);
    }
}
