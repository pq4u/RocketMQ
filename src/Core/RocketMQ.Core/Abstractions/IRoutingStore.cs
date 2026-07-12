using RocketMQ.Core.Models;

namespace RocketMQ.Core.Abstractions;

/// <summary>
/// Port for routing metadata persistence. Stores exchange, queue, and
/// binding definitions. Implementations (SQLite, WAL, etc.) must satisfy
/// an identical contract verified by RoutingStoreContractTests.
///
/// CONTRACT:
///
/// 1. Idempotency: DeclareExchangeAsync and DeclareQueueAsync are
///    idempotent — declaring an existing entity with the same configuration
///    is a no-op. Declaring with DIFFERENT configuration throws
///    InvalidOperationException.
///
/// 2. Referential integrity: BindAsync must throw InvalidOperationException
///    if the exchange or queue does not exist.
///
/// 3. Delete semantics: DeleteExchangeAsync/DeleteQueueAsync must also
///    remove all associated bindings. Deleting a non-existent entity is a
///    no-op (no exception).
///
/// 4. Durability: same as IMessageQueueStore — once a Task completes, the
///    metadata survives a process crash.
///
/// 5. Concurrency safety: all methods must be safe to call concurrently.
/// </summary>
public interface IRoutingStore
{
    // ── Exchanges ──

    /// <summary>
    /// Declares an exchange. Idempotent if config matches.
    /// Contract point: 1 (idempotency), 4 (durability), 5 (concurrency).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an exchange with the same name but different configuration already exists.
    /// </exception>
    Task DeclareExchangeAsync(Exchange exchange, CancellationToken ct);

    /// <summary>
    /// Deletes an exchange and all its bindings. No-op if the exchange does not exist.
    /// Contract point: 3 (delete semantics).
    /// </summary>
    Task DeleteExchangeAsync(string exchangeName, CancellationToken ct);

    /// <summary>Returns the exchange or null if it does not exist.</summary>
    Task<Exchange?> GetExchangeAsync(string exchangeName, CancellationToken ct);

    /// <summary>Returns all declared exchanges.</summary>
    Task<IReadOnlyList<Exchange>> ListExchangesAsync(CancellationToken ct);

    // ── Queues ──

    /// <summary>
    /// Declares a queue. Idempotent if config matches.
    /// Contract point: 1 (idempotency), 4 (durability), 5 (concurrency).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a queue with the same name but different configuration already exists.
    /// </exception>
    Task DeclareQueueAsync(QueueDefinition queue, CancellationToken ct);

    /// <summary>
    /// Deletes a queue and all its bindings. No-op if the queue does not exist.
    /// Contract point: 3 (delete semantics).
    /// </summary>
    Task DeleteQueueAsync(string queueName, CancellationToken ct);

    /// <summary>Returns the queue definition or null if it does not exist.</summary>
    Task<QueueDefinition?> GetQueueAsync(string queueName, CancellationToken ct);

    /// <summary>Returns all declared queues.</summary>
    Task<IReadOnlyList<QueueDefinition>> ListQueuesAsync(CancellationToken ct);

    // ── Bindings ──

    /// <summary>
    /// Creates a binding from an exchange to a queue with a routing key.
    /// Contract point: 2 (referential integrity — exchange and queue must exist).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the exchange or queue does not exist.
    /// </exception>
    Task BindAsync(Binding binding, CancellationToken ct);

    /// <summary>
    /// Removes a specific binding. No-op if the binding does not exist.
    /// </summary>
    Task UnbindAsync(string exchangeName, string queueName, string routingKey, CancellationToken ct);

    /// <summary>Returns all bindings for a given exchange.</summary>
    Task<IReadOnlyList<Binding>> GetBindingsAsync(string exchangeName, CancellationToken ct);
}
