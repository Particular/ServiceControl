namespace ServiceControl.Audit.Persistence.PostgreSQL.UnitOfWork;

using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using ServiceControl.Audit.Auditing;
using ServiceControl.Audit.Monitoring;
using ServiceControl.Audit.Persistence.Monitoring;
using ServiceControl.Audit.Persistence.UnitOfWork;
using ServiceControl.SagaAudit;

class PostgreSQLAuditIngestionUnitOfWork : IAuditIngestionUnitOfWork
{
    readonly NpgsqlBatch batch;
    readonly NpgsqlConnection connection;

    public PostgreSQLAuditIngestionUnitOfWork(NpgsqlConnection connection)
    {
        batch = new NpgsqlBatch(connection);
        this.connection = connection;
    }

    public async ValueTask DisposeAsync()
    {
        await batch.PrepareAsync();
        await batch.ExecuteNonQueryAsync();
        await batch.DisposeAsync();
        await connection.DisposeAsync();
    }

    public Task RecordProcessedMessage(ProcessedMessage processedMessage, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        T GetMetadata<T>(string key) => processedMessage.MessageMetadata.TryGetValue(key, out var value) ? (T)value ?? default : default;

        // Insert ProcessedMessage into processed_messages table
        var cmd = batch.CreateBatchCommand();
        cmd.CommandText = @"
                INSERT INTO processed_messages (
                    unique_message_id, message_metadata, headers, processed_at, body,
                    message_id, message_type, is_system_message, status, time_sent, receiving_endpoint_name,
                    critical_time, processing_time, delivery_time, conversation_id
                ) VALUES (
                    @unique_message_id, @message_metadata, @headers, @processed_at, @body,
                    @message_id, @message_type, @is_system_message, @status, @time_sent, @receiving_endpoint_name,
                    @critical_time, @processing_time, @delivery_time, @conversation_id
                );";

        processedMessage.MessageMetadata["ContentLength"] = body.Length;
        if (!body.IsEmpty)
        {
            cmd.Parameters.AddWithValue("body", body);
        }
        else
        {
            cmd.Parameters.AddWithValue("body", DBNull.Value);
        }
        cmd.Parameters.AddWithValue("unique_message_id", processedMessage.UniqueMessageId);
        cmd.Parameters.AddWithValue("message_metadata", NpgsqlTypes.NpgsqlDbType.Jsonb, processedMessage.MessageMetadata);
        cmd.Parameters.AddWithValue("headers", NpgsqlTypes.NpgsqlDbType.Jsonb, processedMessage.Headers);
        cmd.Parameters.AddWithValue("processed_at", processedMessage.ProcessedAt);
        cmd.Parameters.AddWithValue("message_id", GetMetadata<string>("MessageId"));
        cmd.Parameters.AddWithValue("message_type", GetMetadata<string>("MessageType"));
        cmd.Parameters.AddWithValue("is_system_message", GetMetadata<bool>("IsSystemMessage"));
        cmd.Parameters.AddWithValue("time_sent", GetMetadata<DateTime>("TimeSent"));
        cmd.Parameters.AddWithValue("receiving_endpoint_name", GetMetadata<EndpointDetails>("ReceivingEndpoint")?.Name ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("critical_time", GetMetadata<TimeSpan>("CriticalTime"));
        cmd.Parameters.AddWithValue("processing_time", GetMetadata<TimeSpan>("ProcessingTime"));
        cmd.Parameters.AddWithValue("delivery_time", GetMetadata<TimeSpan>("DeliveryTime"));
        cmd.Parameters.AddWithValue("conversation_id", GetMetadata<string>("ConversationId"));
        cmd.Parameters.AddWithValue("status", (int)(GetMetadata<bool>("IsRetried") ? MessageStatus.ResolvedSuccessfully : MessageStatus.Successful));

        batch.BatchCommands.Add(cmd);
        return Task.CompletedTask;
    }

    public Task RecordSagaSnapshot(SagaSnapshot sagaSnapshot, CancellationToken cancellationToken)
    {
        // Insert SagaSnapshot into saga_snapshots table
        var cmd = batch.CreateBatchCommand();
        cmd.CommandText = @"
                INSERT INTO saga_snapshots (
                    id, saga_id, saga_type, start_time, finish_time, status, state_after_change, initiating_message, outgoing_messages, endpoint, processed_at
                ) VALUES (
                    @id, @saga_id, @saga_type, @start_time, @finish_time, @status, @state_after_change, @initiating_message, @outgoing_messages, @endpoint, @processed_at
                )
                ON CONFLICT (id) DO NOTHING;";

        cmd.Parameters.AddWithValue("id", sagaSnapshot.Id);
        cmd.Parameters.AddWithValue("saga_id", sagaSnapshot.SagaId);
        cmd.Parameters.AddWithValue("saga_type", sagaSnapshot.SagaType);
        cmd.Parameters.AddWithValue("start_time", sagaSnapshot.StartTime);
        cmd.Parameters.AddWithValue("finish_time", sagaSnapshot.FinishTime);
        cmd.Parameters.AddWithValue("status", sagaSnapshot.Status.ToString());
        cmd.Parameters.AddWithValue("state_after_change", sagaSnapshot.StateAfterChange);
        cmd.Parameters.AddWithValue("initiating_message", NpgsqlTypes.NpgsqlDbType.Jsonb, sagaSnapshot.InitiatingMessage);
        cmd.Parameters.AddWithValue("outgoing_messages", NpgsqlTypes.NpgsqlDbType.Jsonb, sagaSnapshot.OutgoingMessages);
        cmd.Parameters.AddWithValue("endpoint", sagaSnapshot.Endpoint);
        cmd.Parameters.AddWithValue("processed_at", sagaSnapshot.ProcessedAt);
        batch.BatchCommands.Add(cmd);

        return Task.CompletedTask;
    }

    public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint, CancellationToken cancellationToken)
    {
        // Insert KnownEndpoint into known_endpoints table
        var cmd = batch.CreateBatchCommand();
        cmd.CommandText = @"
                INSERT INTO known_endpoints (
                    id, name, host_id, host, last_seen
                ) VALUES (
                    @id, @name, @host_id, @host, @last_seen
                )
                ON CONFLICT (id) DO NOTHING;";

        cmd.Parameters.AddWithValue("id", knownEndpoint.Id);
        cmd.Parameters.AddWithValue("name", knownEndpoint.Name);
        cmd.Parameters.AddWithValue("host_id", knownEndpoint.HostId);
        cmd.Parameters.AddWithValue("host", knownEndpoint.Host);
        cmd.Parameters.AddWithValue("last_seen", knownEndpoint.LastSeen);
        batch.BatchCommands.Add(cmd);

        return Task.CompletedTask;
    }
}