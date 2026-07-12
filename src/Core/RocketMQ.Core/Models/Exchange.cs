namespace RocketMQ.Core.Models;

/// <summary>
/// An exchange definition — the entry point for message routing.
/// Producers publish to an exchange with a routing key; the exchange
/// type determines which bound queues receive the message.
/// </summary>
/// <param name="Name">Unique exchange name. Empty string = default exchange.</param>
/// <param name="Type">Routing algorithm (Direct, Fanout, Topic).</param>
/// <param name="Durable">If true, survives broker restart.</param>
public sealed record Exchange(
    string Name,
    ExchangeType Type,
    bool Durable);
