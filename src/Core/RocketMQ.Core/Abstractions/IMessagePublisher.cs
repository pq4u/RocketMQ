using RocketMQ.Core.Models;

namespace RocketMQ.Core.Abstractions;

/// <summary>Durably publishes a routed message and returns its routing outcome.</summary>
public interface IMessagePublisher
{
    Task<PublishResult> PublishAsync(Guid publishId, Envelope envelope, CancellationToken ct);
}
