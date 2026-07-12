using System;
using System.Threading;
using System.Threading.Tasks;

namespace RocketMQ.Client;

public enum ConsumeResult { Success, Requeue, DeadLetter }

public record MessageContext(string LeaseId, ReadOnlyMemory<byte> Payload, int DeliveryCount, string CorrelationId);

public interface IConsumer : IAsyncDisposable
{
    Task StartConsumingAsync(string queueName, Func<MessageContext, Task<ConsumeResult>> handler, CancellationToken ct = default);
}
