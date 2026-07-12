namespace RocketMQ.Core.Models;

/// <summary>
/// A binding connects an exchange to a queue with a routing key pattern.
/// The interpretation of the routing key depends on the exchange type
/// (exact match for Direct, ignored for Fanout, wildcard for Topic).
/// </summary>
/// <param name="ExchangeName">Source exchange.</param>
/// <param name="QueueName">Target queue.</param>
/// <param name="RoutingKey">Routing key or pattern.</param>
public sealed record Binding(
    string ExchangeName,
    string QueueName,
    string RoutingKey);
