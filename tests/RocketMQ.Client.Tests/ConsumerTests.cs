using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using RocketMQ.Client;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Client.Tests;

public sealed class ConsumerTests
{
    [Theory]
    [InlineData(ConsumeResult.Success, "Ack")]
    [InlineData(ConsumeResult.Requeue, "Nack")]
    public async Task ConsumeLoop_AckAndNackIncludeQueueName(
        ConsumeResult result,
        string expectedRpc)
    {
        var invoker = new RecordingConsumerCallInvoker();
        var grpcClient = new RocketMQ.Transport.Grpc.Protos.Consumer.ConsumerClient(invoker);
        await using var consumer = new Consumer(grpcClient, NullLogger<Consumer>.Instance);

        await consumer.StartConsumingAsync(
            "orders-workers",
            _ => Task.FromResult(result),
            TestContext.Current.CancellationToken);

        var completion = await invoker.Completion.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        Assert.Equal(expectedRpc, completion.Rpc);
        Assert.Equal("orders-workers", completion.QueueName);
    }

    private sealed class RecordingConsumerCallInvoker : CallInvoker
    {
        private int _leaseCalls;

        public TaskCompletionSource<(string Rpc, string QueueName)> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request)
        {
            object response = method.Name switch
            {
                "LeaseNext" when Interlocked.Increment(ref _leaseCalls) == 1 => new LeaseResponse
                {
                    LeaseId = Guid.NewGuid().ToString(),
                    MessageId = Guid.NewGuid().ToString(),
                    Payload = Google.Protobuf.ByteString.CopyFromUtf8("payload"),
                    DeliveryCount = 1,
                    CorrelationId = Guid.NewGuid().ToString()
                },
                "LeaseNext" => new LeaseResponse(),
                "Ack" => Complete("Ack", ((AckRequest)(object)request).QueueName),
                "Nack" => Complete("Nack", ((NackRequest)(object)request).QueueName),
                _ => throw new NotSupportedException(method.FullName)
            };

            return new AsyncUnaryCall<TResponse>(
                Task.FromResult((TResponse)response),
                Task.FromResult(new Metadata()),
                static () => Status.DefaultSuccess,
                static () => new Metadata(),
                static () => { });
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request)
            => throw new NotSupportedException();

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options)
            => throw new NotSupportedException();

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request)
            => throw new NotSupportedException();

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options)
            => throw new NotSupportedException();

        private AckResponse Complete(string rpc, string queueName)
        {
            Completion.TrySetResult((rpc, queueName));
            return new AckResponse();
        }
    }
}
