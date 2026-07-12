using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;

namespace RocketMQ.Core.Routing;

/// <summary>
/// Routes messages to destination queues by evaluating exchange bindings.
/// Supports <see cref="ExchangeType.Fanout"/>, <see cref="ExchangeType.Direct"/>,
/// and <see cref="ExchangeType.Topic"/> exchange types.
/// </summary>
public sealed class MessageRouter : IMessageRouter
{
    private readonly IRoutingStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageRouter"/> class.
    /// </summary>
    /// <param name="store">The routing store used to look up exchanges and bindings.</param>
    public MessageRouter(IRoutingStore store) => _store = store;

    /// <summary>
    /// Resolves the set of destination queue names for a message published to the
    /// specified exchange with the given routing key.
    /// </summary>
    /// <param name="exchangeName">The name of the target exchange.</param>
    /// <param name="routingKey">The routing key attached to the message.</param>
    /// <param name="ct">A token to cancel the asynchronous operation.</param>
    /// <returns>A distinct list of queue names that the message should be delivered to.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the exchange does not exist or has an unknown type.
    /// </exception>
    public async Task<IReadOnlyList<string>> ResolveAsync(
        string exchangeName, string routingKey, CancellationToken ct)
    {
        var exchange = await _store.GetExchangeAsync(exchangeName, ct)
            ?? throw new InvalidOperationException(
                $"Exchange '{exchangeName}' does not exist.");

        var bindings = await _store.GetBindingsAsync(exchangeName, ct);

        return exchange.Type switch
        {
            ExchangeType.Fanout => bindings
                .Select(b => b.QueueName)
                .Distinct()
                .ToList(),

            ExchangeType.Direct => bindings
                .Where(b => b.RoutingKey == routingKey)
                .Select(b => b.QueueName)
                .Distinct()
                .ToList(),

            ExchangeType.Topic => bindings
                .Where(b => TopicMatcher.Matches(b.RoutingKey, routingKey))
                .Select(b => b.QueueName)
                .Distinct()
                .ToList(),

            _ => throw new InvalidOperationException(
                $"Unknown exchange type: {exchange.Type}")
        };
    }
}
