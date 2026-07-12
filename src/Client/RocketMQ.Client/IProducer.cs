using System;
using System.Threading;
using System.Threading.Tasks;

namespace RocketMQ.Client;

public interface IProducer
{
    Task PublishAsync(string exchangeName, string routingKey, ReadOnlyMemory<byte> payload, string? correlationId = null, CancellationToken ct = default);
}
