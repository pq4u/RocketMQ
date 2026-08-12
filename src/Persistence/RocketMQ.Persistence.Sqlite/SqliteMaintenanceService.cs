namespace RocketMQ.Persistence.Sqlite;

/// <summary>Performs bounded operational cleanup for the SQLite backend.</summary>
public sealed class SqliteMaintenanceService
{
    private readonly SqliteDatabase _database;

    public SqliteMaintenanceService(SqliteDatabase database) => _database = database;

    public Task PurgeDeadLettersAsync(TimeSpan retention, CancellationToken ct)
    {
        if (retention < TimeSpan.FromDays(1) || retention > TimeSpan.FromDays(365))
        {
            throw new ArgumentOutOfRangeException(nameof(retention), "Dead-letter retention must be between 1 and 365 days.");
        }

        return _database.WriteAsync(async (connection, transaction, token) =>
        {
            await SqliteDatabase.ExecuteNonQueryAsync(connection, transaction,
                "DELETE FROM messages WHERE state='dead_lettered' AND dead_lettered_at_utc < $cutoff;", token,
                ("$cutoff", SqliteDatabase.UtcText(DateTimeOffset.UtcNow.Subtract(retention))));
            return 0;
        }, ct);
    }
}
