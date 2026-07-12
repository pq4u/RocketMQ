namespace RocketMQ.Core.Abstractions;

/// <summary>
/// Resolves which queues a message should be routed to based on
/// exchange type and bindings. Core business logic, not a persistence port.
/// See ADR-0002 for the full routing model.
/// </summary>
public interface IMessageRouter
{
    /// <summary>
    /// Given an exchange name and routing key, returns the list of queue
    /// names the message should be delivered to.
    /// Returns empty list if exchange exists but no bindings match.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the exchange does not exist.
    /// </exception>
    Task<IReadOnlyList<string>> ResolveAsync(
        string exchangeName,
        string routingKey,
        CancellationToken ct);
}
