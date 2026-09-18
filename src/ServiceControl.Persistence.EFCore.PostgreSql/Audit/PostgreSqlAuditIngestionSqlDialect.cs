namespace ServiceControl.Persistence.EFCore.PostgreSql.Audit;

using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.Audit;

// Plain inserts, chunked to keep the statement text down to a few reusable shapes. The identity
// column is left to the database and never read back.
class PostgreSqlAuditIngestionSqlDialect : PostgreSqlDialect, IAuditIngestionSqlDialect
{
    public async Task InsertAuditMessages(ServiceControlDbContext dbContext, IReadOnlyList<AuditMessageEntity> rows, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in rows.Chunk(MaxRowsPerStatement))
        {
            await Execute(
                dbContext,
                $"""
                 INSERT INTO {Table<AuditMessageEntity>(dbContext)} ({AuditMessageColumnList})
                 VALUES
                 {ParameterRows(chunk.Length, AuditMessageColumns.Length)}
                 """,
                chunk.Select(AuditMessageValues),
                cancellationToken);
        }
    }

    public async Task InsertSagaSnapshots(ServiceControlDbContext dbContext, IReadOnlyList<SagaSnapshotEntity> rows, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in rows.Chunk(MaxRowsPerStatement))
        {
            await Execute(
                dbContext,
                $"""
                 INSERT INTO {Table<SagaSnapshotEntity>(dbContext)} ({SagaSnapshotColumnList})
                 VALUES
                 {ParameterRows(chunk.Length, SagaSnapshotColumns.Length)}
                 """,
                chunk.Select(SagaSnapshotValues),
                cancellationToken);
        }
    }

    // Column order matches AuditMessageValues
    static readonly string[] AuditMessageColumns =
    [
        "created_on", "unique_message_id", "message_id", "message_type", "time_sent", "processed_at",
        "conversation_id", "is_system_message", "status",
        "sending_endpoint_name", "sending_endpoint_host_id", "sending_endpoint_host",
        "receiving_endpoint_name", "receiving_endpoint_host_id", "receiving_endpoint_host",
        "critical_time_ticks", "processing_time_ticks", "delivery_time_ticks",
        "headers_json", "body_text", "body_stored_externally", "body_size", "body_content_type"
    ];

    static object?[] AuditMessageValues(AuditMessageEntity row) =>
    [
        row.CreatedOn, row.UniqueMessageId, row.MessageId, row.MessageType, row.TimeSent, row.ProcessedAt,
        row.ConversationId, row.IsSystemMessage, (int)row.Status,
        row.SendingEndpointName, row.SendingEndpointHostId, row.SendingEndpointHost,
        row.ReceivingEndpointName, row.ReceivingEndpointHostId, row.ReceivingEndpointHost,
        row.CriticalTimeTicks, row.ProcessingTimeTicks, row.DeliveryTimeTicks,
        row.HeadersJson, row.BodyText, row.BodyStoredExternally, row.BodySize, row.BodyContentType
    ];

    // Column order matches SagaSnapshotValues
    static readonly string[] SagaSnapshotColumns =
    [
        "created_on", "saga_id", "saga_type", "status", "start_time", "finish_time", "processed_at",
        "endpoint", "state_after_change", "initiating_message_json", "outgoing_messages_json"
    ];

    static object?[] SagaSnapshotValues(SagaSnapshotEntity row) =>
    [
        row.CreatedOn, row.SagaId, row.SagaType, (int)row.Status, row.StartTime, row.FinishTime, row.ProcessedAt,
        row.Endpoint, row.StateAfterChange, row.InitiatingMessageJson, row.OutgoingMessagesJson
    ];

    static readonly string AuditMessageColumnList = string.Join(", ", AuditMessageColumns);

    static readonly string SagaSnapshotColumnList = string.Join(", ", SagaSnapshotColumns);
}
