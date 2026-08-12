namespace RocketMQ.Client;

public interface IProducer
{
    Task<PublishResult> PublishAsync(string exchangeName, string routingKey, ReadOnlyMemory<byte> payload, string? correlationId = null, Guid? publishId = null, CancellationToken ct = default);
}

public sealed record PublishResult(Guid PublishId, Guid MessageId, string Status, IReadOnlyList<string> DestinationQueues)
{
    public bool Accepted => StringComparer.Ordinal.Equals(Status, "Accepted");
}
