using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Diagnostics;
using RocketMQ.Core.Models;
using RocketMQ.Core.Routing;

namespace RocketMQ.Persistence.Sqlite;

public sealed class SqliteMessagePublisher : IMessagePublisher
{
    private readonly SqliteDatabase _database;

    public SqliteMessagePublisher(SqliteDatabase database) => _database = database;

    public Task<PublishResult> PublishAsync(Guid publishId, Envelope envelope, CancellationToken ct)
        => _database.WriteAsync(async (connection, transaction, token) =>
        {
            var diagnostics = Activity.Current?.GetTagItem(PublishDiagnosticTags.Enabled) is true
                ? Activity.Current
                : null;
            var stageStarted = Stopwatch.GetTimestamp();
            await SqliteDatabase.ExecuteNonQueryAsync(connection, transaction, "DELETE FROM publications WHERE created_at_utc < $cutoff;", token, ("$cutoff", SqliteDatabase.UtcText(DateTimeOffset.UtcNow.AddHours(-24))));
            SqliteDatabase.SetElapsed(diagnostics, PublishDiagnosticTags.CleanupMilliseconds, stageStarted);

            stageStarted = Stopwatch.GetTimestamp();
            var fingerprint = Fingerprint(envelope);
            SqliteDatabase.SetElapsed(diagnostics, PublishDiagnosticTags.FingerprintMilliseconds, stageStarted);

            stageStarted = Stopwatch.GetTimestamp();
            var existing = await FindPublicationAsync(connection, transaction, publishId, token);
            SqliteDatabase.SetElapsed(diagnostics, PublishDiagnosticTags.IdempotencyLookupMilliseconds, stageStarted);
            if (existing is not null)
            {
                if (!StringComparer.Ordinal.Equals(existing.Value.Fingerprint, fingerprint))
                {
                    throw new InvalidOperationException("Publish ID was already used with different message data.");
                }

                stageStarted = Stopwatch.GetTimestamp();
                var existingResult = await ReadResultAsync(connection, transaction, publishId, existing.Value.MessageId, existing.Value.Status, token);
                SqliteDatabase.SetElapsed(diagnostics, PublishDiagnosticTags.ResultReadMilliseconds, stageStarted);
                return existingResult;
            }

            stageStarted = Stopwatch.GetTimestamp();
            var exchange = await FindExchangeAsync(connection, transaction, envelope.ExchangeName, token)
                ?? throw new KeyNotFoundException($"Exchange '{envelope.ExchangeName}' does not exist.");
            SqliteDatabase.SetElapsed(diagnostics, PublishDiagnosticTags.ExchangeLookupMilliseconds, stageStarted);

            stageStarted = Stopwatch.GetTimestamp();
            var destinations = await ResolveDestinationsAsync(connection, transaction, exchange, envelope.RoutingKey, token);
            SqliteDatabase.SetElapsed(diagnostics, PublishDiagnosticTags.RoutingMilliseconds, stageStarted);
            var messageId = Guid.NewGuid();
            var status = destinations.Count == 0 ? PublishStatus.Unroutable : PublishStatus.Accepted;

            stageStarted = Stopwatch.GetTimestamp();
            await SqliteDatabase.ExecuteNonQueryAsync(connection, transaction, """
                INSERT INTO publications(publish_id, message_id, request_fingerprint, status, created_at_utc)
                VALUES ($publishId, $messageId, $fingerprint, $status, $createdAt);
                """, token,
                ("$publishId", SqliteDatabase.GuidBytes(publishId)),
                ("$messageId", SqliteDatabase.GuidBytes(messageId)),
                ("$fingerprint", fingerprint),
                ("$status", (int)status),
                ("$createdAt", SqliteDatabase.UtcNowText()));
            SqliteDatabase.SetElapsed(diagnostics, PublishDiagnosticTags.PublicationInsertMilliseconds, stageStarted);

            stageStarted = Stopwatch.GetTimestamp();
            foreach (var queueName in destinations)
            {
                await SqliteMessageQueueStore.InsertMessageAsync(connection, transaction, queueName, messageId, envelope.Message, token);
                await SqliteDatabase.ExecuteNonQueryAsync(connection, transaction,
                    "INSERT INTO publication_destinations(publish_id, queue_name) VALUES ($publishId, $queue);", token,
                    ("$publishId", SqliteDatabase.GuidBytes(publishId)), ("$queue", queueName));
            }
            SqliteDatabase.SetElapsed(diagnostics, PublishDiagnosticTags.EnqueueMilliseconds, stageStarted);
            return new PublishResult(publishId, messageId, status, destinations);
        }, ct);

    private static async Task<(Guid MessageId, string Fingerprint, PublishStatus Status)?> FindPublicationAsync(SqliteConnection connection, SqliteTransaction transaction, Guid publishId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT message_id, request_fingerprint, status FROM publications WHERE publish_id=$publishId;";
        command.Parameters.AddWithValue("$publishId", SqliteDatabase.GuidBytes(publishId));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? (SqliteDatabase.ReadGuid(reader, 0), reader.GetString(1), (PublishStatus)reader.GetInt32(2))
            : null;
    }

    private static async Task<PublishResult> ReadResultAsync(SqliteConnection connection, SqliteTransaction transaction, Guid publishId, Guid messageId, PublishStatus status, CancellationToken ct)
    {
        var destinations = new List<string>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT queue_name FROM publication_destinations WHERE publish_id=$publishId ORDER BY queue_name;";
        command.Parameters.AddWithValue("$publishId", SqliteDatabase.GuidBytes(publishId));
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) destinations.Add(reader.GetString(0));
        return new PublishResult(publishId, messageId, status, destinations);
    }

    private static async Task<Exchange?> FindExchangeAsync(SqliteConnection connection, SqliteTransaction transaction, string name, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT name, type, durable FROM exchanges WHERE name=$name;";
        command.Parameters.AddWithValue("$name", name);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new Exchange(reader.GetString(0), (ExchangeType)reader.GetInt32(1), reader.GetInt32(2) != 0) : null;
    }

    private static async Task<IReadOnlyList<string>> ResolveDestinationsAsync(SqliteConnection connection, SqliteTransaction transaction, Exchange exchange, string routingKey, CancellationToken ct)
    {
        var bindings = new List<Binding>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT exchange_name, queue_name, routing_key FROM bindings WHERE exchange_name=$exchange;";
        command.Parameters.AddWithValue("$exchange", exchange.Name);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) bindings.Add(new Binding(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return exchange.Type switch
        {
            ExchangeType.Fanout => bindings.Select(x => x.QueueName).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            ExchangeType.Direct => bindings.Where(x => x.RoutingKey == routingKey).Select(x => x.QueueName).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            ExchangeType.Topic => bindings.Where(x => TopicMatcher.Matches(x.RoutingKey, routingKey)).Select(x => x.QueueName).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            _ => throw new InvalidOperationException($"Unknown exchange type: {exchange.Type}")
        };
    }

    private static string Fingerprint(Envelope envelope)
    {
        var metadata = Encoding.UTF8.GetBytes($"{envelope.ExchangeName}\n{envelope.RoutingKey}\n{envelope.Message.CorrelationId:N}\n");
        var data = new byte[metadata.Length + envelope.Message.Payload.Length];
        metadata.CopyTo(data, 0);
        envelope.Message.Payload.Span.CopyTo(data.AsSpan(metadata.Length));
        return Convert.ToHexString(SHA256.HashData(data));
    }
}



