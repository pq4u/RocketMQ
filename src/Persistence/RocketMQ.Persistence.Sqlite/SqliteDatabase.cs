using System.Globalization;
using Microsoft.Data.Sqlite;

namespace RocketMQ.Persistence.Sqlite;

/// <summary>Owns SQLite initialization and serializes all database mutations.</summary>
public sealed class SqliteDatabase
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private readonly SemaphoreSlim _writerGate = new(1, 1);
    private bool _initialized;

    public SqliteDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    internal async Task WriteAsync(Func<SqliteConnection, SqliteTransaction, CancellationToken, Task<int>> action, CancellationToken ct)
    {
        await WriteAsync<int>(action, ct);
    }

    internal async Task<T> WriteAsync<T>(
        Func<SqliteConnection, SqliteTransaction, CancellationToken, Task<T>> action,
        CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        await _writerGate.WaitAsync(ct);
        try
        {
            await using var connection = await OpenConnectionAsync(ct);
            using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
            try
            {
                var result = await action(connection, transaction, ct);
                transaction.Commit();
                return result;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        finally
        {
            _writerGate.Release();
        }
    }

    internal async Task<T> ReadAsync<T>(Func<SqliteConnection, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        await using var connection = await OpenConnectionAsync(ct);
        return await action(connection, ct);
    }

    internal async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await ConfigureConnectionAsync(connection, ct);
        return connection;
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationGate.WaitAsync(ct);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);
            await ConfigureConnectionAsync(connection, ct);
            using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
            try
            {
                await ExecuteNonQueryAsync(connection, transaction, """
                    CREATE TABLE IF NOT EXISTS schema_migrations (
                        version INTEGER PRIMARY KEY,
                        applied_at_utc TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS exchanges (
                        name TEXT PRIMARY KEY,
                        type INTEGER NOT NULL,
                        durable INTEGER NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS queues (
                        name TEXT PRIMARY KEY,
                        durable INTEGER NOT NULL CHECK (durable = 1),
                        max_delivery_count INTEGER NOT NULL CHECK (max_delivery_count >= 0)
                    );
                    CREATE TABLE IF NOT EXISTS bindings (
                        exchange_name TEXT NOT NULL REFERENCES exchanges(name) ON DELETE CASCADE,
                        queue_name TEXT NOT NULL REFERENCES queues(name) ON DELETE CASCADE,
                        routing_key TEXT NOT NULL,
                        PRIMARY KEY (exchange_name, queue_name, routing_key)
                    );
                    CREATE TABLE IF NOT EXISTS messages (
                        message_row_id INTEGER PRIMARY KEY AUTOINCREMENT,
                        message_id BLOB NOT NULL,
                        queue_name TEXT NOT NULL REFERENCES queues(name) ON DELETE CASCADE,
                        connection_id BLOB NOT NULL,
                        correlation_id BLOB NOT NULL,
                        payload BLOB NOT NULL,
                        received_at_utc TEXT NOT NULL,
                        enqueued_at_utc TEXT NOT NULL,
                        state TEXT NOT NULL,
                        lease_id BLOB NULL UNIQUE,
                        lease_expires_at_utc TEXT NULL,
                        delivery_count INTEGER NOT NULL DEFAULT 0,
                        dead_lettered_at_utc TEXT NULL,
                        dead_letter_reason TEXT NULL
                    );
                    CREATE INDEX IF NOT EXISTS ix_messages_lease ON messages(queue_name, state, enqueued_at_utc, message_row_id);
                    CREATE INDEX IF NOT EXISTS ix_messages_lease_id ON messages(lease_id);
                    CREATE INDEX IF NOT EXISTS ix_messages_dead_letters ON messages(queue_name, state, dead_lettered_at_utc);
                    CREATE INDEX IF NOT EXISTS ix_bindings_exchange ON bindings(exchange_name);
                    CREATE TABLE IF NOT EXISTS persistence_log (
                        sequence INTEGER PRIMARY KEY AUTOINCREMENT,
                        connection_id BLOB NOT NULL,
                        correlation_id BLOB NOT NULL,
                        payload BLOB NOT NULL,
                        received_at_utc TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS publications (
                        publish_id BLOB PRIMARY KEY,
                        message_id BLOB NOT NULL,
                        request_fingerprint TEXT NOT NULL,
                        status INTEGER NOT NULL,
                        created_at_utc TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS publication_destinations (
                        publish_id BLOB NOT NULL REFERENCES publications(publish_id) ON DELETE CASCADE,
                        queue_name TEXT NOT NULL,
                        PRIMARY KEY (publish_id, queue_name)
                    );
                    """, ct);
                await ExecuteNonQueryAsync(connection, transaction,
                    "INSERT OR IGNORE INTO schema_migrations(version, applied_at_utc) VALUES (1, $now);",
                    ct, ("$now", UtcNowText()));
                transaction.Commit();
                _initialized = true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private static async Task ConfigureConnectionAsync(SqliteConnection connection, CancellationToken ct)
    {
        await ExecuteNonQueryAsync(connection, null, "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;", ct);
    }

    internal static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        CancellationToken ct,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        await command.ExecuteNonQueryAsync(ct);
    }

    internal static string UtcNowText() => DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    internal static string UtcText(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    internal static DateTimeOffset ReadUtc(SqliteDataReader reader, int ordinal) => DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    internal static byte[] GuidBytes(Guid value) => value.ToByteArray();
    internal static Guid ReadGuid(SqliteDataReader reader, int ordinal) => new((byte[])reader.GetValue(ordinal));
}

