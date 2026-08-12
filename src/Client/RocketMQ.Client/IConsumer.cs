using System;
using System.Threading;
using System.Threading.Tasks;

namespace RocketMQ.Client;

public enum ConsumeResult
{
    Success,
    Requeue,
    DeadLetter
}

public sealed class ConsumerOptions
{
    public TimeSpan VisibilityTimeout { get; init; } = TimeSpan.FromSeconds(30);

    internal int VisibilityTimeoutSeconds
    {
        get
        {
            if (VisibilityTimeout < TimeSpan.FromSeconds(1) ||
                VisibilityTimeout > TimeSpan.FromHours(1))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(VisibilityTimeout),
                    "Visibility timeout must be between one second and one hour.");
            }

            return checked((int)Math.Ceiling(VisibilityTimeout.TotalSeconds));
        }
    }
}

public record MessageContext(
    string LeaseId,
    ReadOnlyMemory<byte> Payload,
    int DeliveryCount,
    string CorrelationId)
{
    public string MessageId { get; init; } = string.Empty;
}

public interface IConsumer : IAsyncDisposable
{
    Task StartConsumingAsync(
        string queueName,
        Func<MessageContext, Task<ConsumeResult>> handler,
        CancellationToken ct = default);

    Task StartConsumingAsync(
        string queueName,
        Func<MessageContext, Task<ConsumeResult>> handler,
        ConsumerOptions options,
        CancellationToken ct = default);
}
