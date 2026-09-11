using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;

namespace RocketMQ.Persistence.Sqlite;

public sealed class SqliteMessageQueueStore : IMessageQueueStore
{
    private readonly SqliteDatabase _database;

    public SqliteMessageQueueStore(string connectionString) : this(new SqliteDatabase(connectionString)) { }
    public SqliteMessageQueueStore(SqliteDatabase database) => _database = database;

    public Task<Guid> EnqueueAsync(string queueName, InboundMessage message, CancellationToken ct)
        => EnqueueAsync(queueName, Guid.NewGuid(), message, ct);

    internal Task<Guid> EnqueueAsync(string queueName, Guid messageId, InboundMessage message, CancellationToken ct)
        => _database.WriteAsync(async (connection, transaction, token) =>
        {
            await EnsureQueueExistsAsync(connection, transaction, queueName, token);
            await InsertMessageAsync(connection, transaction, queueName, messageId, message, token);
            return messageId;
        }, ct);

    public Task<LeasedMessage?> LeaseNextAsync(
        string queueName,
        TimeSpan visibilityTimeout,
        CancellationToken ct)
        => LeaseNextAsync(queueName, visibilityTimeout, leaseOwnerId: null, ct);

    public Task<LeasedMessage?> LeaseNextAsync(
        string queueName,
        TimeSpan visibilityTimeout,
        string? leaseOwnerId,
        CancellationToken ct)
    {
        if (visibilityTimeout <= TimeSpan.Zero || visibilityTimeout == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(visibilityTimeout), "Visibility timeout must be greater than zero and finite.");

        return _database.WriteAsync(async (connection, transaction, token) =>
        {
            var now = DateTimeOffset.UtcNow;
            while (true)
            {
                await using var select = connection.CreateCommand();
                select.Transaction = transaction;
                select.CommandText = """
                    SELECT m.message_row_id, m.message_id, m.connection_id, m.correlation_id, m.payload,
                           m.received_at_utc, m.delivery_count, q.max_delivery_count
                    FROM messages m
                    JOIN queues q ON q.name = m.queue_name
                    WHERE m.queue_name = $queue
                      AND (m.state = 'available' OR (m.state = 'leased' AND m.lease_expires_at_utc <= $now))
                    ORDER BY m.enqueued_at_utc, m.message_row_id
                    LIMIT 1;
                    """;
                select.Parameters.AddWithValue("$queue", queueName);
                select.Parameters.AddWithValue("$now", SqliteDatabase.UtcText(now));
                await using var reader = await select.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) return null;

                var rowId = reader.GetInt64(0);
                var messageId = SqliteDatabase.ReadGuid(reader, 1);
                var message = new InboundMessage(
                    SqliteDatabase.ReadGuid(reader, 2),
                    SqliteDatabase.ReadGuid(reader, 3),
                    (byte[])reader.GetValue(4),
                    SqliteDatabase.ReadUtc(reader, 5));
                var deliveryCount = reader.GetInt32(6);
                var maxDeliveryCount = reader.GetInt32(7);
                await reader.DisposeAsync();

                if (maxDeliveryCount > 0 && deliveryCount >= maxDeliveryCount)
                {
                    await SqliteDatabase.ExecuteNonQueryAsync(connection, transaction, """
                        UPDATE messages SET state='dead_lettered', lease_id=NULL, lease_expires_at_utc=NULL,
                        lease_owner_id=NULL,
                        dead_lettered_at_utc=$now, dead_letter_reason='max-delivery-count-exceeded'
                        WHERE message_row_id=$id;
                        """, token, ("$now", SqliteDatabase.UtcText(now)), ("$id", rowId));
                    continue;
                }

                var leaseId = Guid.NewGuid();
                var expiresAt = now.Add(visibilityTimeout);
                await SqliteDatabase.ExecuteNonQueryAsync(connection, transaction, """
                    UPDATE messages SET state='leased', lease_id=$leaseId, lease_expires_at_utc=$expiresAt,
                    lease_owner_id=$leaseOwnerId, delivery_count=delivery_count+1 WHERE message_row_id=$id;
                    """, token,
                    ("$leaseId", SqliteDatabase.GuidBytes(leaseId)),
                    ("$expiresAt", SqliteDatabase.UtcText(expiresAt)),
                    ("$leaseOwnerId", leaseOwnerId),
                    ("$id", rowId));
                return new LeasedMessage(messageId, leaseId, message, deliveryCount + 1, expiresAt);
            }
        }, ct);
    }

    public Task<string> GetActiveLeaseQueueAsync(
        Guid leaseId,
        string? leaseOwnerId,
        CancellationToken ct)
        => _database.ReadAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT queue_name, lease_expires_at_utc
                FROM messages
                WHERE lease_id=$leaseId
                  AND state='leased'
                  AND lease_owner_id IS $leaseOwnerId;
                """;
            command.Parameters.AddWithValue("$leaseId", SqliteDatabase.GuidBytes(leaseId));
            command.Parameters.AddWithValue("$leaseOwnerId", (object?)leaseOwnerId ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                throw new InvalidOperationException("Lease not found.");
            }

            var queueName = reader.GetString(0);
            var expiresAt = SqliteDatabase.ReadUtc(reader, 1);
            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                throw new InvalidOperationException("Lease expired.");
            }

            return queueName;
        }, ct);

    public Task AckAsync(Guid leaseId, CancellationToken ct)
        => AckAsync(leaseId, leaseOwnerId: null, expectedQueueName: null, ct);

    public Task AckAsync(
        Guid leaseId,
        string? leaseOwnerId,
        string? expectedQueueName,
        CancellationToken ct)
        => CompleteLeaseAsync(leaseId, requeue: null, leaseOwnerId, expectedQueueName, ct);

    public Task NackAsync(Guid leaseId, bool requeue, CancellationToken ct)
        => NackAsync(leaseId, requeue, leaseOwnerId: null, expectedQueueName: null, ct);

    public Task NackAsync(
        Guid leaseId,
        bool requeue,
        string? leaseOwnerId,
        string? expectedQueueName,
        CancellationToken ct)
        => CompleteLeaseAsync(leaseId, requeue, leaseOwnerId, expectedQueueName, ct);

    private Task CompleteLeaseAsync(
        Guid leaseId,
        bool? requeue,
        string? leaseOwnerId,
        string? expectedQueueName,
        CancellationToken ct) => _database.WriteAsync(async (connection, transaction, token) =>
    {
        var now = DateTimeOffset.UtcNow;
        var queuePredicate = expectedQueueName is null ? string.Empty : " AND queue_name=$queueName";
        var sql = requeue switch
        {
            null => "DELETE FROM messages WHERE lease_id=$leaseId AND state='leased' AND lease_expires_at_utc > $now AND lease_owner_id IS $leaseOwnerId" + queuePredicate + ";",
            true => "UPDATE messages SET state='available', lease_id=NULL, lease_expires_at_utc=NULL, lease_owner_id=NULL WHERE lease_id=$leaseId AND state='leased' AND lease_expires_at_utc > $now AND lease_owner_id IS $leaseOwnerId" + queuePredicate + ";",
            false => "UPDATE messages SET state='dead_lettered', lease_id=NULL, lease_expires_at_utc=NULL, lease_owner_id=NULL, dead_lettered_at_utc=$now, dead_letter_reason='consumer-rejected' WHERE lease_id=$leaseId AND state='leased' AND lease_expires_at_utc > $now AND lease_owner_id IS $leaseOwnerId" + queuePredicate + ";"
        };
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$leaseId", SqliteDatabase.GuidBytes(leaseId));
        command.Parameters.AddWithValue("$now", SqliteDatabase.UtcText(now));
        command.Parameters.AddWithValue("$leaseOwnerId", (object?)leaseOwnerId ?? DBNull.Value);
        if (expectedQueueName is not null)
        {
            command.Parameters.AddWithValue("$queueName", expectedQueueName);
        }

        if (await command.ExecuteNonQueryAsync(token) != 1)
        {
            await ThrowLeaseFailureAsync(
                connection,
                transaction,
                leaseId,
                leaseOwnerId,
                expectedQueueName,
                now,
                token);
        }

        return 0;
    }, ct);

    private static async Task ThrowLeaseFailureAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        Guid leaseId,
        string? leaseOwnerId,
        string? expectedQueueName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT lease_expires_at_utc
            FROM messages
            WHERE lease_id=$leaseId
              AND state='leased'
              AND lease_owner_id IS $leaseOwnerId
              AND ($queueName IS NULL OR queue_name=$queueName);
            """;
        command.Parameters.AddWithValue("$leaseId", SqliteDatabase.GuidBytes(leaseId));
        command.Parameters.AddWithValue("$leaseOwnerId", (object?)leaseOwnerId ?? DBNull.Value);
        command.Parameters.AddWithValue("$queueName", (object?)expectedQueueName ?? DBNull.Value);
        var expiresAtValue = await command.ExecuteScalarAsync(ct);
        if (expiresAtValue is string expiresAtText
            && DateTimeOffset.Parse(
                expiresAtText,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind) <= now)
        {
            throw new InvalidOperationException("Lease expired.");
        }

        throw new InvalidOperationException("Lease not found.");
    }

    public IAsyncEnumerable<DeadLetteredMessage> BrowseDeadLettersAsync(string queueName, CancellationToken ct)
        => BrowseDeadLettersCoreAsync(queueName, ct);

    private async IAsyncEnumerable<DeadLetteredMessage> BrowseDeadLettersCoreAsync(
        string queueName,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var rows = await _database.ReadAsync(async (connection, token) =>
        {
            var result = new List<DeadLetteredMessage>();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT message_id, connection_id, correlation_id, payload, received_at_utc,
                       delivery_count, dead_lettered_at_utc, dead_letter_reason
                FROM messages WHERE queue_name=$queue AND state='dead_lettered'
                ORDER BY dead_lettered_at_utc, message_row_id;
                """;
            command.Parameters.AddWithValue("$queue", queueName);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                result.Add(new DeadLetteredMessage(
                    SqliteDatabase.ReadGuid(reader, 0),
                    new InboundMessage(SqliteDatabase.ReadGuid(reader, 1), SqliteDatabase.ReadGuid(reader, 2), (byte[])reader.GetValue(3), SqliteDatabase.ReadUtc(reader, 4)),
                    reader.GetInt32(5), SqliteDatabase.ReadUtc(reader, 6), reader.GetString(7)));
            }
            return result;
        }, ct);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    internal static async Task EnsureQueueExistsAsync(Microsoft.Data.Sqlite.SqliteConnection connection, Microsoft.Data.Sqlite.SqliteTransaction transaction, string queueName, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM queues WHERE name=$name);";
        command.Parameters.AddWithValue("$name", queueName);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) == 0)
            throw new InvalidOperationException($"Queue '{queueName}' does not exist.");
    }

    internal static Task InsertMessageAsync(Microsoft.Data.Sqlite.SqliteConnection connection, Microsoft.Data.Sqlite.SqliteTransaction transaction, string queueName, Guid messageId, InboundMessage message, CancellationToken ct)
        => SqliteDatabase.ExecuteNonQueryAsync(connection, transaction, """
            INSERT INTO messages(message_id, queue_name, connection_id, correlation_id, payload, received_at_utc, enqueued_at_utc, state)
            VALUES ($messageId, $queue, $connectionId, $correlationId, $payload, $receivedAt, $enqueuedAt, 'available');
            """, ct,
            ("$messageId", SqliteDatabase.GuidBytes(messageId)),
            ("$queue", queueName),
            ("$connectionId", SqliteDatabase.GuidBytes(message.ConnectionId)),
            ("$correlationId", SqliteDatabase.GuidBytes(message.CorrelationId)),
            ("$payload", message.Payload.ToArray()),
            ("$receivedAt", SqliteDatabase.UtcText(message.ReceivedAtUtc)),
            ("$enqueuedAt", SqliteDatabase.UtcNowText()));
}
