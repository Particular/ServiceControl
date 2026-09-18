namespace ServiceControl.Persistence.EFCore.SqlServer.Audit;

using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.Audit;

// Plain inserts, chunked to stay under the parameter ceiling. The identity column is left to the
// database and never read back.
class SqlServerAuditIngestionSqlDialect : SqlServerDialect, IAuditIngestionSqlDialect
{
    public async Task InsertAuditMessages(ServiceControlDbContext dbContext, IReadOnlyList<AuditMessageEntity> rows, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in rows.Chunk(MaxRowsPerStatement(AuditMessageColumns.Length)))
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
        foreach (var chunk in rows.Chunk(MaxRowsPerStatement(SagaSnapshotColumns.Length)))
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
        "[CreatedOn]", "[UniqueMessageId]", "[MessageId]", "[MessageType]", "[TimeSent]", "[ProcessedAt]",
        "[ConversationId]", "[IsSystemMessage]", "[Status]",
        "[SendingEndpointName]", "[SendingEndpointHostId]", "[SendingEndpointHost]",
        "[ReceivingEndpointName]", "[ReceivingEndpointHostId]", "[ReceivingEndpointHost]",
        "[CriticalTimeTicks]", "[ProcessingTimeTicks]", "[DeliveryTimeTicks]",
        "[HeadersJson]", "[BodyText]", "[BodyStoredExternally]", "[BodySize]", "[BodyContentType]"
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
        "[CreatedOn]", "[SagaId]", "[SagaType]", "[Status]", "[StartTime]", "[FinishTime]", "[ProcessedAt]",
        "[Endpoint]", "[StateAfterChange]", "[InitiatingMessageJson]", "[OutgoingMessagesJson]"
    ];

    static object?[] SagaSnapshotValues(SagaSnapshotEntity row) =>
    [
        row.CreatedOn, row.SagaId, row.SagaType, (int)row.Status, row.StartTime, row.FinishTime, row.ProcessedAt,
        row.Endpoint, row.StateAfterChange, row.InitiatingMessageJson, row.OutgoingMessagesJson
    ];

    static readonly string AuditMessageColumnList = string.Join(", ", AuditMessageColumns);

    static readonly string SagaSnapshotColumnList = string.Join(", ", SagaSnapshotColumns);
}
