
namespace ServiceControl.Audit.Persistence.PostgreSQL;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Npgsql;

class PostgreSQLPersistenceInstaller(DatabaseConfiguration databaseConfiguration, PostgreSQLConnectionFactory connectionFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var adminConnection = await connectionFactory.OpenAdminConnection(cancellationToken);

        await using (var cmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = @dbname", adminConnection))
        {
            cmd.Parameters.AddWithValue("@dbname", databaseConfiguration.Name);
            var exists = await cmd.ExecuteScalarAsync(cancellationToken);
            if (exists == null)
            {
                using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{databaseConfiguration.Name}\"", adminConnection);
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await using var connection = await connectionFactory.OpenConnection(cancellationToken);
        // Create processed_messages table
        await using (var cmd = new NpgsqlCommand(@"
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
                        query tsvector,
                        created_at TIMESTAMPTZ NOT NULL DEFAULT now()
                    )
                    WITH (
                        autovacuum_vacuum_scale_factor = 0.05,
                        autovacuum_analyze_scale_factor = 0.02
                    );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Create trigger for full text search
        await using (var cmd = new NpgsqlCommand(@"
            CREATE OR REPLACE FUNCTION processed_messages_tsvector_update() RETURNS trigger AS $$
            BEGIN
            NEW.query :=
                setweight(to_tsvector('english', coalesce(NEW.headers::text, '')), 'A') ||
                setweight(to_tsvector('english', coalesce(convert_from(NEW.body, 'UTF8'), '')), 'B');
            RETURN NEW;
            END
            $$ LANGUAGE plpgsql;

            DROP TRIGGER IF EXISTS processed_messages_tsvector_trigger ON processed_messages;
            CREATE TRIGGER processed_messages_tsvector_trigger
            BEFORE INSERT OR UPDATE ON processed_messages
            FOR EACH ROW EXECUTE FUNCTION processed_messages_tsvector_update();", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Create index on processed_messages for specified columns
        await using (var cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_processed_messages_receiving_endpoint_name ON processed_messages (
                receiving_endpoint_name
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_processed_messages_is_system_message ON processed_messages (
                is_system_message
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_processed_messages_by_time_sent ON processed_messages (
                time_sent
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_processed_messages_by_critical_time ON processed_messages (
                critical_time
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_processed_messages_by_processing_time ON processed_messages (
                processing_time
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_processed_messages_by_delivery_time ON processed_messages (
                delivery_time
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_processed_messages_by_message_id ON processed_messages (
                message_id
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }


        await using (var cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_processed_messages_by_created_at ON processed_messages (
                created_at
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_processed_messages_by_conversation ON processed_messages (
                conversation_id,
                time_sent
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_processed_messages_by_query ON processed_messages (
                query
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_processed_messages_audit_counts ON processed_messages (
                receiving_endpoint_name, processed_at
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Create saga_snapshots table
        await using (var cmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS saga_snapshots (
                id UUID PRIMARY KEY,
                saga_id UUID,
                saga_type TEXT,
                changes JSONB,
                updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Create index on saga_snapshots for faster saga_id lookups
        await using (var cmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS idx_saga_snapshots_saga_id ON saga_snapshots (
                saga_id
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Create known_endpoints table
        await using (var cmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS known_endpoints (
                id TEXT PRIMARY KEY,
                name TEXT,
                host_id UUID,
                host TEXT,
                last_seen TIMESTAMPTZ,
                updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Create trigger to auto-update updated_at for saga_snapshots
        await using (var cmd = new NpgsqlCommand(@"
            CREATE OR REPLACE FUNCTION update_updated_at_column() RETURNS trigger AS $$
            BEGIN
            NEW.updated_at = now();
            RETURN NEW;
            END
            $$ LANGUAGE plpgsql;

            DROP TRIGGER IF EXISTS saga_snapshots_updated_at_trigger ON saga_snapshots;
            CREATE TRIGGER saga_snapshots_updated_at_trigger
            BEFORE UPDATE ON saga_snapshots
            FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

            DROP TRIGGER IF EXISTS known_endpoints_updated_at_trigger ON known_endpoints;
            CREATE TRIGGER known_endpoints_updated_at_trigger
            BEFORE UPDATE ON known_endpoints
            FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}