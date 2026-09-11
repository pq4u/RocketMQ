using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;

namespace RocketMQ.Persistence.Sqlite;

public sealed class SqliteQueueManagementStore : IQueueManagementStore
{
    private readonly SqliteDatabase _database;

    public SqliteQueueManagementStore(SqliteDatabase database) => _database = database;

    public Task<QueueStatistics> GetStatisticsAsync(string queueName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        return _database.ReadAsync(async (connection, token) =>
        {
            var now = SqliteDatabase.UtcNowText();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT q.name,
                       COALESCE(SUM(CASE WHEN m.state='available'
                                             OR (m.state='leased' AND m.lease_expires_at_utc <= $now)
                                         THEN 1 ELSE 0 END), 0),
                       COALESCE(SUM(CASE WHEN m.state='leased' AND m.lease_expires_at_utc > $now
                                         THEN 1 ELSE 0 END), 0),
                       COALESCE(SUM(CASE WHEN m.state='dead_lettered' THEN 1 ELSE 0 END), 0),
                       MIN(CASE WHEN m.state='available'
                                      OR (m.state='leased' AND m.lease_expires_at_utc <= $now)
                                THEN m.enqueued_at_utc END)
                FROM queues q
                LEFT JOIN messages m ON m.queue_name=q.name
                WHERE q.name=$queue
                GROUP BY q.name;
                """;
            command.Parameters.AddWithValue("$queue", queueName);
            command.Parameters.AddWithValue("$now", now);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                throw new KeyNotFoundException($"Queue '{queueName}' does not exist.");
            }

            return new QueueStatistics(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                reader.IsDBNull(4) ? null : SqliteDatabase.ReadUtc(reader, 4));
        }, ct);
    }

    public Task<MessagePage> BrowseReadyAsync(
        string queueName,
        string? cursor,
        int limit,
        CancellationToken ct)
        => BrowseAsync(queueName, cursor, limit, deadLetters: false, ct);

    public Task<InspectedMessage?> GetReadyAsync(
        string queueName,
        Guid messageId,
        int maxPayloadBytes,
        CancellationToken ct)
        => GetAsync(queueName, messageId, maxPayloadBytes, deadLetter: false, ct);

    public Task<MessagePage> BrowseDeadLettersAsync(
        string queueName,
        string? cursor,
        int limit,
        CancellationToken ct)
        => BrowseAsync(queueName, cursor, limit, deadLetters: true, ct);

    public Task<InspectedMessage?> GetDeadLetterAsync(
        string queueName,
        Guid messageId,
        int maxPayloadBytes,
        CancellationToken ct)
        => GetAsync(queueName, messageId, maxPayloadBytes, deadLetter: true, ct);

    public Task<int> PurgeReadyAsync(string queueName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        return _database.WriteAsync<int>(async (connection, transaction, token) =>
        {
            await EnsureQueueExistsAsync(connection, transaction, queueName, token);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                DELETE FROM messages
                WHERE queue_name=$queue
                  AND (state='available' OR (state='leased' AND lease_expires_at_utc <= $now));
                """;
            command.Parameters.AddWithValue("$queue", queueName);
            command.Parameters.AddWithValue("$now", SqliteDatabase.UtcNowText());
            return await command.ExecuteNonQueryAsync(token);
        }, ct);
    }

    public Task<MessageMutationResult> RequeueDeadLetterAsync(
        string queueName,
        Guid messageId,
        CancellationToken ct)
        => MutateDeadLetterAsync(queueName, messageId, delete: false, ct);

    public Task<MessageMutationResult> DeleteDeadLetterAsync(
        string queueName,
        Guid messageId,
        CancellationToken ct)
        => MutateDeadLetterAsync(queueName, messageId, delete: true, ct);

    public Task<int> ClearDeadLettersAsync(string queueName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        return _database.WriteAsync<int>(async (connection, transaction, token) =>
        {
            await EnsureQueueExistsAsync(connection, transaction, queueName, token);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM messages WHERE queue_name=$queue AND state='dead_lettered';";
            command.Parameters.AddWithValue("$queue", queueName);
            return await command.ExecuteNonQueryAsync(token);
        }, ct);
    }

    public Task<bool> CheckHealthAsync(CancellationToken ct)
        => _database.ReadAsync(async (connection, token) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            return Convert.ToInt32(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture) == 1;
        }, ct);

    private Task<MessagePage> BrowseAsync(
        string queueName,
        string? cursor,
        int limit,
        bool deadLetters,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Page size must be between 1 and 100.");
        }

        var decodedCursor = DecodeCursor(cursor, deadLetters);
        return _database.ReadAsync(async (connection, token) =>
        {
            await EnsureQueueExistsAsync(connection, transaction: null, queueName, token);
            var current = decodedCursor ?? await CreateFirstCursorAsync(connection, queueName, deadLetters, token);
            var sortColumn = deadLetters ? "dead_lettered_at_utc" : "enqueued_at_utc";
            var statePredicate = deadLetters
                ? "state='dead_lettered'"
                : "(state='available' OR (state='leased' AND lease_expires_at_utc <= $asOf))";

            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT message_row_id, message_id, correlation_id, received_at_utc, enqueued_at_utc,
                       delivery_count, length(payload), lease_expires_at_utc,
                       dead_lettered_at_utc, dead_letter_reason, {sortColumn}
                FROM messages
                WHERE queue_name=$queue
                  AND {statePredicate}
                  AND message_row_id <= $highWatermark
                  AND ($lastSort IS NULL OR {sortColumn} > $lastSort
                       OR ({sortColumn} = $lastSort AND message_row_id > $lastRowId))
                ORDER BY {sortColumn}, message_row_id
                LIMIT $take;
                """;
            command.Parameters.AddWithValue("$queue", queueName);
            command.Parameters.AddWithValue("$asOf", current.AsOfUtc);
            command.Parameters.AddWithValue("$highWatermark", current.HighWatermark);
            command.Parameters.AddWithValue("$lastSort", (object?)current.LastSort ?? DBNull.Value);
            command.Parameters.AddWithValue("$lastRowId", current.LastRowId);
            command.Parameters.AddWithValue("$take", limit + 1);

            var rows = new List<(long RowId, string Sort, MessageSummary Summary)>();
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                rows.Add((
                    reader.GetInt64(0),
                    reader.GetString(10),
                    ReadSummary(reader, offset: 1)));
            }

            var hasMore = rows.Count > limit;
            if (hasMore)
            {
                rows.RemoveAt(rows.Count - 1);
            }

            var nextCursor = hasMore && rows.Count > 0
                ? EncodeCursor(current with
                {
                    LastSort = rows[^1].Sort,
                    LastRowId = rows[^1].RowId
                })
                : null;
            return new MessagePage(rows.Select(row => row.Summary).ToArray(), nextCursor);
        }, ct);
    }

    private Task<InspectedMessage?> GetAsync(
        string queueName,
        Guid messageId,
        int maxPayloadBytes,
        bool deadLetter,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        if (maxPayloadBytes is < 1 or > 1_048_576)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPayloadBytes),
                "Payload preview must be between 1 byte and 1 MiB.");
        }

        return _database.ReadAsync(async (connection, token) =>
        {
            await EnsureQueueExistsAsync(connection, transaction: null, queueName, token);
            var statePredicate = deadLetter
                ? "state='dead_lettered'"
                : "(state='available' OR (state='leased' AND lease_expires_at_utc <= $now))";
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT message_id, correlation_id, received_at_utc, enqueued_at_utc,
                       delivery_count, length(payload), lease_expires_at_utc,
                       dead_lettered_at_utc, dead_letter_reason, substr(payload, 1, $payloadBytes)
                FROM messages
                WHERE queue_name=$queue AND message_id=$messageId AND {statePredicate}
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$queue", queueName);
            command.Parameters.AddWithValue("$messageId", SqliteDatabase.GuidBytes(messageId));
            command.Parameters.AddWithValue("$now", SqliteDatabase.UtcNowText());
            command.Parameters.AddWithValue("$payloadBytes", maxPayloadBytes);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                return null;
            }

            return new InspectedMessage(ReadSummary(reader), (byte[])reader.GetValue(9));
        }, ct);
    }

    private Task<MessageMutationResult> MutateDeadLetterAsync(
        string queueName,
        Guid messageId,
        bool delete,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        return _database.WriteAsync(async (connection, transaction, token) =>
        {
            await EnsureQueueExistsAsync(connection, transaction, queueName, token);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = delete
                ? "DELETE FROM messages WHERE queue_name=$queue AND message_id=$messageId AND state='dead_lettered';"
                : """
                  UPDATE messages
                  SET state='available', lease_id=NULL, lease_expires_at_utc=NULL, lease_owner_id=NULL,
                      delivery_count=0, dead_lettered_at_utc=NULL, dead_letter_reason=NULL
                  WHERE queue_name=$queue AND message_id=$messageId AND state='dead_lettered';
                  """;
            command.Parameters.AddWithValue("$queue", queueName);
            command.Parameters.AddWithValue("$messageId", SqliteDatabase.GuidBytes(messageId));
            if (await command.ExecuteNonQueryAsync(token) == 1)
            {
                return MessageMutationResult.Succeeded;
            }

            await using var check = connection.CreateCommand();
            check.Transaction = transaction;
            check.CommandText = "SELECT EXISTS(SELECT 1 FROM messages WHERE queue_name=$queue AND message_id=$messageId);";
            check.Parameters.AddWithValue("$queue", queueName);
            check.Parameters.AddWithValue("$messageId", SqliteDatabase.GuidBytes(messageId));
            var exists = Convert.ToInt32(await check.ExecuteScalarAsync(token), CultureInfo.InvariantCulture) != 0;
            return exists ? MessageMutationResult.InvalidState : MessageMutationResult.NotFound;
        }, ct);
    }

    private static MessageSummary ReadSummary(SqliteDataReader reader, int offset = 0)
        => new(
            SqliteDatabase.ReadGuid(reader, offset),
            SqliteDatabase.ReadGuid(reader, offset + 1),
            SqliteDatabase.ReadUtc(reader, offset + 2),
            SqliteDatabase.ReadUtc(reader, offset + 3),
            reader.GetInt32(offset + 4),
            reader.GetInt32(offset + 5),
            reader.IsDBNull(offset + 6) ? null : SqliteDatabase.ReadUtc(reader, offset + 6),
            reader.IsDBNull(offset + 7) ? null : SqliteDatabase.ReadUtc(reader, offset + 7),
            reader.IsDBNull(offset + 8) ? null : reader.GetString(offset + 8));

    private static async Task<BrowseCursor> CreateFirstCursorAsync(
        SqliteConnection connection,
        string queueName,
        bool deadLetters,
        CancellationToken ct)
    {
        var asOf = SqliteDatabase.UtcNowText();
        var statePredicate = deadLetters
            ? "state='dead_lettered'"
            : "(state='available' OR (state='leased' AND lease_expires_at_utc <= $asOf))";
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COALESCE(MAX(message_row_id), 0) FROM messages WHERE queue_name=$queue AND {statePredicate};";
        command.Parameters.AddWithValue("$queue", queueName);
        command.Parameters.AddWithValue("$asOf", asOf);
        var highWatermark = Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        return new BrowseCursor(deadLetters, asOf, highWatermark, null, 0);
    }

    private static string EncodeCursor(BrowseCursor cursor)
        => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(cursor))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static BrowseCursor? DecodeCursor(string? cursor, bool expectedDeadLetters)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
            var decoded = JsonSerializer.Deserialize<BrowseCursor>(Convert.FromBase64String(base64));
            if (decoded is null
                || decoded.DeadLetters != expectedDeadLetters
                || decoded.HighWatermark < 0
                || decoded.LastRowId < 0
                || !DateTimeOffset.TryParse(
                    decoded.AsOfUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _))
            {
                throw new FormatException();
            }

            return decoded;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new ArgumentException("Cursor is invalid.", nameof(cursor));
        }
    }

    private static async Task EnsureQueueExistsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string queueName,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM queues WHERE name=$queue);";
        command.Parameters.AddWithValue("$queue", queueName);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) == 0)
        {
            throw new KeyNotFoundException($"Queue '{queueName}' does not exist.");
        }
    }

    private sealed record BrowseCursor(
        bool DeadLetters,
        string AsOfUtc,
        long HighWatermark,
        string? LastSort,
        long LastRowId);
}
