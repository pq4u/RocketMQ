using Microsoft.Data.Sqlite;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;

namespace RocketMQ.Persistence.Sqlite;

public sealed class SqliteRoutingStore : IRoutingStore
{
    private readonly SqliteDatabase _database;

    public SqliteRoutingStore(string connectionString) : this(new SqliteDatabase(connectionString)) { }
    public SqliteRoutingStore(SqliteDatabase database) => _database = database;

    public Task DeclareExchangeAsync(Exchange exchange, CancellationToken ct) => _database.WriteAsync(async (connection, transaction, token) =>
    {
        await using var query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = "SELECT type, durable FROM exchanges WHERE name = $name;";
        query.Parameters.AddWithValue("$name", exchange.Name);
        await using var reader = await query.ExecuteReaderAsync(token);
        if (await reader.ReadAsync(token))
        {
            if (reader.GetInt32(0) != (int)exchange.Type || reader.GetInt32(1) != (exchange.Durable ? 1 : 0))
            {
                throw new InvalidOperationException($"Exchange '{exchange.Name}' exists with different configuration.");
            }
            return 0;
        }
        await reader.DisposeAsync();
        await SqliteDatabase.ExecuteNonQueryAsync(connection, transaction,
            "INSERT INTO exchanges(name, type, durable) VALUES ($name, $type, $durable);", token,
            ("$name", exchange.Name), ("$type", (int)exchange.Type), ("$durable", exchange.Durable ? 1 : 0));
        return 0;
    }, ct);

    public Task DeleteExchangeAsync(string exchangeName, CancellationToken ct) => _database.WriteAsync(async (connection, transaction, token) =>
    {
        await SqliteDatabase.ExecuteNonQueryAsync(connection, transaction, "DELETE FROM exchanges WHERE name = $name;", token, ("$name", exchangeName));
        return 0;
    }, ct);

    public Task<Exchange?> GetExchangeAsync(string exchangeName, CancellationToken ct) => _database.ReadAsync(async (connection, token) =>
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, type, durable FROM exchanges WHERE name = $name;";
        command.Parameters.AddWithValue("$name", exchangeName);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? new Exchange(reader.GetString(0), (ExchangeType)reader.GetInt32(1), reader.GetInt32(2) != 0) : null;
    }, ct);

    public Task<IReadOnlyList<Exchange>> ListExchangesAsync(CancellationToken ct) => _database.ReadAsync(async (connection, token) =>
    {
        var result = new List<Exchange>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, type, durable FROM exchanges ORDER BY name;";
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(new Exchange(reader.GetString(0), (ExchangeType)reader.GetInt32(1), reader.GetInt32(2) != 0));
        return (IReadOnlyList<Exchange>)result;
    }, ct);

    public Task DeclareQueueAsync(QueueDefinition queue, CancellationToken ct)
    {
        if (!queue.Durable) throw new InvalidOperationException("Non-durable queues are not supported.");
        if (queue.MaxDeliveryCount < 0) throw new ArgumentOutOfRangeException(nameof(queue), "MaxDeliveryCount cannot be negative.");
        return _database.WriteAsync(async (connection, transaction, token) =>
        {
            await using var query = connection.CreateCommand();
            query.Transaction = transaction;
            query.CommandText = "SELECT durable, max_delivery_count FROM queues WHERE name = $name;";
            query.Parameters.AddWithValue("$name", queue.Name);
            await using var reader = await query.ExecuteReaderAsync(token);
            if (await reader.ReadAsync(token))
            {
                if (reader.GetInt32(0) != 1 || reader.GetInt32(1) != queue.MaxDeliveryCount)
                    throw new InvalidOperationException($"Queue '{queue.Name}' exists with different configuration.");
                return 0;
            }
            await reader.DisposeAsync();
            await SqliteDatabase.ExecuteNonQueryAsync(connection, transaction,
                "INSERT INTO queues(name, durable, max_delivery_count) VALUES ($name, 1, $max);", token,
                ("$name", queue.Name), ("$max", queue.MaxDeliveryCount));
            return 0;
        }, ct);
    }

    public Task DeleteQueueAsync(string queueName, CancellationToken ct) => _database.WriteAsync(async (connection, transaction, token) =>
    {
        await SqliteDatabase.ExecuteNonQueryAsync(connection, transaction, "DELETE FROM queues WHERE name = $name;", token, ("$name", queueName));
        return 0;
    }, ct);

    public Task<QueueDefinition?> GetQueueAsync(string queueName, CancellationToken ct) => _database.ReadAsync(async (connection, token) =>
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, durable, max_delivery_count FROM queues WHERE name = $name;";
        command.Parameters.AddWithValue("$name", queueName);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? new QueueDefinition(reader.GetString(0), reader.GetInt32(1) != 0, reader.GetInt32(2)) : null;
    }, ct);

    public Task<IReadOnlyList<QueueDefinition>> ListQueuesAsync(CancellationToken ct) => _database.ReadAsync(async (connection, token) =>
    {
        var result = new List<QueueDefinition>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, durable, max_delivery_count FROM queues ORDER BY name;";
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(new QueueDefinition(reader.GetString(0), reader.GetInt32(1) != 0, reader.GetInt32(2)));
        return (IReadOnlyList<QueueDefinition>)result;
    }, ct);

    public Task BindAsync(Binding binding, CancellationToken ct) => _database.WriteAsync(async (connection, transaction, token) =>
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM exchanges WHERE name = $exchange), EXISTS(SELECT 1 FROM queues WHERE name = $queue);";
        command.Parameters.AddWithValue("$exchange", binding.ExchangeName);
        command.Parameters.AddWithValue("$queue", binding.QueueName);
        await using var reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        if (reader.GetInt32(0) == 0 || reader.GetInt32(1) == 0) throw new InvalidOperationException("Exchange or queue does not exist.");
        await reader.DisposeAsync();
        await SqliteDatabase.ExecuteNonQueryAsync(connection, transaction,
            "INSERT OR IGNORE INTO bindings(exchange_name, queue_name, routing_key) VALUES ($exchange, $queue, $key);", token,
            ("$exchange", binding.ExchangeName), ("$queue", binding.QueueName), ("$key", binding.RoutingKey));
        return 0;
    }, ct);

    public Task UnbindAsync(string exchangeName, string queueName, string routingKey, CancellationToken ct) => _database.WriteAsync(async (connection, transaction, token) =>
    {
        await SqliteDatabase.ExecuteNonQueryAsync(connection, transaction, "DELETE FROM bindings WHERE exchange_name=$exchange AND queue_name=$queue AND routing_key=$key;", token,
            ("$exchange", exchangeName), ("$queue", queueName), ("$key", routingKey));
        return 0;
    }, ct);

    public Task<IReadOnlyList<Binding>> GetBindingsAsync(string exchangeName, CancellationToken ct) => _database.ReadAsync(async (connection, token) =>
    {
        var result = new List<Binding>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT exchange_name, queue_name, routing_key FROM bindings WHERE exchange_name = $name ORDER BY queue_name, routing_key;";
        command.Parameters.AddWithValue("$name", exchangeName);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(new Binding(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return (IReadOnlyList<Binding>)result;
    }, ct);
}
