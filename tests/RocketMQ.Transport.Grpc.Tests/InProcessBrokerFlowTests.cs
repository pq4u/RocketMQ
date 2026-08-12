using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;
using RocketMQ.Core.Routing;
using RocketMQ.Runner;
using RocketMQ.Transport.Grpc.Protos;
using RocketMQ.Transport.Grpc.Services;

namespace RocketMQ.Transport.Grpc.Tests;

public sealed class InProcessBrokerFlowTests
{
    [Fact]
    public async Task Publish_Route_Lease_And_Ack_Removes_Message()
    {
        var routingStore = new InMemoryRoutingStore();
        var queueStore = new InMemoryMessageQueueStore(routingStore);
        var context = new TestServerCallContext();
        var admin = new AdminService(routingStore);
        var producer = new ProducerService(new TestPublisher(routingStore, queueStore));
        var consumer = new ConsumerService(queueStore);
        await admin.DeclareExchange(new DeclareExchangeRequest { ExchangeName = "orders", ExchangeType = "Direct" }, context);
        await admin.DeclareQueue(new DeclareQueueRequest { QueueName = "order-workers" }, context);
        await admin.Bind(new BindRequest { ExchangeName = "orders", QueueName = "order-workers", RoutingKey = "created" }, context);

        var correlationId = Guid.NewGuid();
        var publishResponse = await producer.Publish(new PublishRequest
        {
            ExchangeName = "orders", RoutingKey = "created", CorrelationId = correlationId.ToString(),
            PublishId = Guid.NewGuid().ToString(), Payload = Google.Protobuf.ByteString.CopyFromUtf8("order-created")
        }, context);

        Assert.True(publishResponse.Success);
        var lease = await consumer.LeaseNext(new LeaseRequest { QueueName = "order-workers", VisibilityTimeoutSeconds = 30 }, context);
        Assert.Equal(correlationId.ToString(), lease.CorrelationId);
        await consumer.Ack(new AckRequest { LeaseId = lease.LeaseId }, context);
        var afterAck = await consumer.LeaseNext(new LeaseRequest { QueueName = "order-workers", VisibilityTimeoutSeconds = 30 }, context);
        Assert.Empty(afterAck.LeaseId);
    }

    private sealed class TestPublisher(IRoutingStore routingStore, IMessageQueueStore queueStore) : IMessagePublisher
    {
        public async Task<PublishResult> PublishAsync(Guid publishId, Envelope envelope, CancellationToken ct)
        {
            var queues = await new MessageRouter(routingStore).ResolveAsync(envelope.ExchangeName, envelope.RoutingKey, ct);
            var messageId = Guid.NewGuid();
            foreach (var queue in queues) await queueStore.EnqueueAsync(queue, envelope.Message, ct);
            return new PublishResult(publishId, messageId, queues.Count == 0 ? PublishStatus.Unroutable : PublishStatus.Accepted, queues);
        }
    }
}
