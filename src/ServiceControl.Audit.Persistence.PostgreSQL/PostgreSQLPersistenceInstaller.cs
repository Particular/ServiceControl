
namespace ServiceControl.Audit.Persistence.PostgreSQL;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Npgsql;

class PostgreSQLPersistenceInstaller(DatabaseConfiguration databaseConfiguration, PostgreSQLConnectionFactory connectionFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var adminConnection = await connectionFactory.OpenAdminConnection(cancellationToken);

        using (var cmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = @dbname", adminConnection))
        {
            cmd.Parameters.AddWithValue("@dbname", databaseConfiguration.Name);
            var exists = await cmd.ExecuteScalarAsync(cancellationToken);
            if (exists == null)
            {
                using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{databaseConfiguration.Name}\"", adminConnection);
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        using var connection = await connectionFactory.OpenConnection(cancellationToken);
        // Create processed_messages table
        using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS processed_messages (
                    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    unique_message_id TEXT,
                    message_metadata JSONB,
                    headers JSONB,
                    processed_at TIMESTAMPTZ,
                    body BYTEA,
                    message_id TEXT,
                    message_type TEXT,
                    is_system_message BOOLEAN,
                    status NUMERIC,
                    time_sent TIMESTAMPTZ,
                    receiving_endpoint_name TEXT,
                    critical_time INTERVAL,
                    processing_time INTERVAL,
                    delivery_time INTERVAL,
                    conversation_id TEXT,
                    query tsvector GENERATED ALWAYS AS (
                        setweight(to_tsvector('english', coalesce(headers::text, '')), 'A') ||
                        setweight(to_tsvector('english', coalesce(body::text, '')), 'B')
                    ) STORED
                );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Create saga_snapshots table
        using (var cmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS saga_snapshots (
                id TEXT PRIMARY KEY,
                saga_id UUID,
                saga_type TEXT,
                start_time TIMESTAMPTZ,
                finish_time TIMESTAMPTZ,
                status TEXT,
                state_after_change TEXT,
                initiating_message JSONB,
                outgoing_messages JSONB,
                endpoint TEXT,
                processed_at TIMESTAMPTZ
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Create known_endpoints table
        using (var cmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS known_endpoints (
                id TEXT PRIMARY KEY,
                name TEXT,
                host_id UUID,
                host TEXT,
                last_seen TIMESTAMPTZ
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}


