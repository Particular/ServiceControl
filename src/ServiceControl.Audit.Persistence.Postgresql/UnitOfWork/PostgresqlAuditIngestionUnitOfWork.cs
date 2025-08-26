namespace ServiceControl.Audit.Persistence.PostgreSQL.UnitOfWork
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Persistence.Monitoring;
    using ServiceControl.Audit.Persistence.UnitOfWork;
    using ServiceControl.SagaAudit;

    public class PostgreSQLAuditIngestionUnitOfWork : IAuditIngestionUnitOfWork
    {
        readonly NpgsqlConnection connection;
        readonly NpgsqlTransaction transaction;

        public PostgreSQLAuditIngestionUnitOfWork(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        public async Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
        {
            object GetMetadata(string key) => processedMessage.MessageMetadata.TryGetValue(key, out var value) ? value ?? DBNull.Value : DBNull.Value;

            // Insert ProcessedMessage into processed_messages table
            var cmd = new NpgsqlCommand(@"
                INSERT INTO processed_messages (
                    unique_message_id, message_metadata, headers, processed_at, body,
                    message_id, message_type, is_system_message, status, time_sent, receiving_endpoint_name,
                    critical_time, processing_time, delivery_time, conversation_id
                ) VALUES (
                    @unique_message_id, @message_metadata, @headers, @processed_at, @body,
                    @message_id, @message_type, @is_system_message, @status, @time_sent, @receiving_endpoint_name,
                    @critical_time, @processing_time, @delivery_time, @conversation_id
                )
                ;", connection, transaction);

            processedMessage.MessageMetadata["ContentLength"] = body.Length;
            if (!body.IsEmpty)
            {
                cmd.Parameters.AddWithValue("body", body);
            }
            else
            {
                cmd.Parameters.AddWithValue("body", DBNull.Value);
            }
            cmd.Parameters.AddWithValue("unique_message_id", processedMessage.UniqueMessageId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("message_metadata", JsonSerializer.Serialize(processedMessage.MessageMetadata));
            cmd.Parameters.AddWithValue("headers", JsonSerializer.Serialize(processedMessage.Headers));
            cmd.Parameters.AddWithValue("processed_at", processedMessage.ProcessedAt);
            cmd.Parameters.AddWithValue("message_id", GetMetadata("MessageId"));
            cmd.Parameters.AddWithValue("message_type", GetMetadata("MessageType"));
            cmd.Parameters.AddWithValue("is_system_message", GetMetadata("IsSystemMessage"));
            cmd.Parameters.AddWithValue("time_sent", GetMetadata("TimeSent"));
            cmd.Parameters.AddWithValue("receiving_endpoint_name", GetMetadata("ReceivingEndpoint"));
            cmd.Parameters.AddWithValue("critical_time", GetMetadata("CriticalTime"));
            cmd.Parameters.AddWithValue("processing_time", GetMetadata("ProcessingTime"));
            cmd.Parameters.AddWithValue("delivery_time", GetMetadata("DeliveryTime"));
            cmd.Parameters.AddWithValue("conversation_id", GetMetadata("ConversationId"));

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken = default)
        {
            // Insert SagaSnapshot into saga_snapshots table
            var cmd = new NpgsqlCommand(@"
                INSERT INTO saga_snapshots (
                    id, saga_id, saga_type, start_time, finish_time, status, state_after_change, initiating_message, outgoing_messages, endpoint, processed_at
                ) VALUES (
                    @id, @saga_id, @saga_type, @start_time, @finish_time, @status, @state_after_change, @initiating_message, @outgoing_messages, @endpoint, @processed_at
                )
                ON CONFLICT (id) DO NOTHING;", connection, transaction);

            cmd.Parameters.AddWithValue("id", sagaSnapshot.Id ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("saga_id", sagaSnapshot.SagaId);
            cmd.Parameters.AddWithValue("saga_type", sagaSnapshot.SagaType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("start_time", sagaSnapshot.StartTime);
            cmd.Parameters.AddWithValue("finish_time", sagaSnapshot.FinishTime);
            cmd.Parameters.AddWithValue("status", sagaSnapshot.Status.ToString());
            cmd.Parameters.AddWithValue("state_after_change", sagaSnapshot.StateAfterChange ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("initiating_message", JsonSerializer.Serialize(sagaSnapshot.InitiatingMessage));
            cmd.Parameters.AddWithValue("outgoing_messages", JsonSerializer.Serialize(sagaSnapshot.OutgoingMessages));
            cmd.Parameters.AddWithValue("endpoint", sagaSnapshot.Endpoint ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("processed_at", sagaSnapshot.ProcessedAt);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task RecordKnownEndpoint(KnownEndpoint knownEndpoint, CancellationToken cancellationToken = default)
        {
            // Insert KnownEndpoint into known_endpoints table
            var cmd = new NpgsqlCommand(@"
                INSERT INTO known_endpoints (
                    id, name, host_id, host, last_seen
                ) VALUES (
                    @id, @name, @host_id, @host, @last_seen
                )
                ON CONFLICT (id) DO NOTHING;", connection, transaction);

            cmd.Parameters.AddWithValue("id", knownEndpoint.Id ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("name", knownEndpoint.Name ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("host_id", knownEndpoint.HostId);
            cmd.Parameters.AddWithValue("host", knownEndpoint.Host ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("last_seen", knownEndpoint.LastSeen);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
