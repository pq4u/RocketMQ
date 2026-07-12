namespace RocketMQ.Core.Models;

/// <summary>
/// Determines how an exchange routes messages to bound queues.
/// See ADR-0002 for routing rules per type.
/// </summary>
public enum ExchangeType
{
    /// <summary>Exact match: routingKey == binding.routingKey.</summary>
    Direct,

    /// <summary>Broadcast to ALL bound queues (routing key ignored).</summary>
    Fanout,

    /// <summary>Wildcard pattern matching on dot-separated routing keys (* and #).</summary>
    Topic
}
