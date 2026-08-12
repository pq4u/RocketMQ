using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Models;

namespace RocketMQ.Persistence.Sqlite;

public sealed class SqlitePersistenceStore : IPersistenceStore
{
    private readonly SqliteDatabase _database;

    public SqlitePersistenceStore(string connectionString) : this(new SqliteDatabase(connectionString)) { }
    public SqlitePersistenceStore(SqliteDatabase database) => _database = database;

    public Task<long> AppendAsync(InboundMessage message, CancellationToken ct) => _database.WriteAsync(async (connection, transaction, token) =>
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO persistence_log(connection_id, correlation_id, payload, received_at_utc) VALUES ($connectionId, $correlationId, $payload, $receivedAt); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$connectionId", SqliteDatabase.GuidBytes(message.ConnectionId));
        command.Parameters.AddWithValue("$correlationId", SqliteDatabase.GuidBytes(message.CorrelationId));
        command.Parameters.AddWithValue("$payload", message.Payload.ToArray());
        command.Parameters.AddWithValue("$receivedAt", SqliteDatabase.UtcText(message.ReceivedAtUtc));
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
    }, ct);

    public IAsyncEnumerable<InboundMessage> ReadFromAsync(long afterSequenceNumber, CancellationToken ct) => ReadFromCoreAsync(afterSequenceNumber, ct);

    private async IAsyncEnumerable<InboundMessage> ReadFromCoreAsync(long afterSequenceNumber, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var rows = await _database.ReadAsync(async (connection, token) =>
        {
            var result = new List<InboundMessage>();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT connection_id, correlation_id, payload, received_at_utc FROM persistence_log WHERE sequence > $after ORDER BY sequence;";
            command.Parameters.AddWithValue("$after", afterSequenceNumber);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) result.Add(new InboundMessage(SqliteDatabase.ReadGuid(reader, 0), SqliteDatabase.ReadGuid(reader, 1), (byte[])reader.GetValue(2), SqliteDatabase.ReadUtc(reader, 3)));
            return result;
        }, ct);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            yield return row;
        }
    }
}
