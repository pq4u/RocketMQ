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
        var queueStore = new InMemoryMessageQueueStore();
        var channel = new InMemoryMessageChannel();
        var router = new MessageRouter(routingStore);
        var routingWorker = new RoutingWorkerService(channel, router, queueStore);
        var context = new TestServerCallContext();
        var admin = new AdminService(routingStore);
        var producer = new ProducerService(channel);
        var consumer = new ConsumerService(queueStore);

        await admin.DeclareExchange(
            new DeclareExchangeRequest
            {
                ExchangeName = "orders",
                ExchangeType = "Direct"
            },
            context);
        await admin.DeclareQueue(
            new DeclareQueueRequest { QueueName = "order-workers" },
            context);
        await admin.Bind(
            new BindRequest
            {
                ExchangeName = "orders",
                QueueName = "order-workers",
                RoutingKey = "created"
            },
            context);

        await routingWorker.StartAsync(CancellationToken.None);
        try
        {
            var correlationId = Guid.NewGuid();
            var payload = Google.Protobuf.ByteString.CopyFromUtf8("order-created");
            var publishResponse = await producer.Publish(
                new PublishRequest
                {
                    ExchangeName = "orders",
                    RoutingKey = "created",
                    CorrelationId = correlationId.ToString(),
                    Payload = payload
                },
                context);

            Assert.True(publishResponse.Success);

            LeaseResponse? lease = null;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (lease == null && DateTime.UtcNow < deadline)
            {
                var candidate = await consumer.LeaseNext(
                    new LeaseRequest
                    {
                        QueueName = "order-workers",
                        VisibilityTimeoutSeconds = 30
                    },
                    context);

                if (!string.IsNullOrEmpty(candidate.LeaseId))
                {
                    lease = candidate;
                }
                else
                {
                    await Task.Delay(10, TestContext.Current.CancellationToken);
                }
            }

            Assert.NotNull(lease);
            Assert.Equal(correlationId.ToString(), lease.CorrelationId);
            Assert.Equal(payload.ToByteArray(), lease.Payload.ToByteArray());

            await consumer.Ack(new AckRequest { LeaseId = lease.LeaseId }, context);

            var afterAck = await consumer.LeaseNext(
                new LeaseRequest
                {
                    QueueName = "order-workers",
                    VisibilityTimeoutSeconds = 30
                },
                context);

            Assert.Empty(afterAck.LeaseId);
        }
        finally
        {
            channel.Complete();
            await routingWorker.StopAsync(CancellationToken.None);
        }
    }
}