namespace ServiceControl.Audit.Persistence.PostgreSQL
{
    using System.Collections.Generic;
    using Npgsql;
    using ServiceControl.Audit.Persistence;

    public class PostgreSQLPersistenceConfiguration : IPersistenceConfiguration
    {
        public string Name => "PostgreSQL";

        public IEnumerable<string> ConfigurationKeys => new[] { "PostgreSqlConnectionString" };

        public IPersistence Create(PersistenceSettings settings)
        {
            settings.PersisterSpecificSettings.TryGetValue("PostgreSqlConnectionString", out var connectionString);
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

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
                        status TEXT,
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
                cmd.ExecuteNonQuery();
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
                cmd.ExecuteNonQuery();
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
                cmd.ExecuteNonQuery();
            }

            return new PostgreSQLPersistence();
        }
    }
}
