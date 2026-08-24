namespace ServiceControl.Persistence.EFCore.PostgreSql;

using System.Text;
using MessageFailures;
using DbContexts;
using Entities;
using Infrastructure;

// INSERT ... ON CONFLICT rather than MERGE: PostgreSQL's MERGE can fail with unique_violation
// when two writers insert the same key concurrently, ON CONFLICT cannot. All references to the
// target table inside DO UPDATE read the pre-update row, so the guards are consistent within one
// atomic statement. Rows are chunked to keep statement texts down to a few reusable shapes.
class PostgreSqlFailedMessageIngestionSqlDialect : PostgreSqlDialect, IFailedMessageIngestionSqlDialect
{
    public async Task UpsertFailedMessages(ServiceControlDbContext dbContext, IReadOnlyList<FailedMessageEntity> rows, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in rows.Chunk(MaxRowsPerStatement))
        {
            await Execute(
                dbContext,
                $"""
                 INSERT INTO failed_messages ({FailedMessageColumnList})
                 VALUES
                 {ParameterRows(chunk.Length, FailedMessageColumns.Length)}
                 {OnConflictUpdate}
                 """,
                chunk.Select(FailedMessageValues),
                cancellationToken);
        }
    }

    public async Task InsertGroups(ServiceControlDbContext dbContext, IReadOnlyList<FailedMessageGroupEntity> rows, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in rows.Chunk(MaxRowsPerStatement))
        {
            await Execute(
                dbContext,
                $"""
                 INSERT INTO failed_message_groups (failed_message_unique_id, group_id, title, type)
                 VALUES
                 {ParameterRows(chunk.Length, 4)}
                 ON CONFLICT (failed_message_unique_id, group_id) DO NOTHING
                 """,
                chunk.Select(group => new object?[] { group.FailedMessageUniqueId, group.GroupId, group.Title, group.Type }),
                cancellationToken);
        }
    }

    public async Task InsertMissingKnownEndpoints(ServiceControlDbContext dbContext, IReadOnlyList<KnownEndpointEntity> rows, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in rows.Chunk(MaxRowsPerStatement))
        {
            await Execute(
                dbContext,
                $"""
                 INSERT INTO known_endpoints (id, name, host_id, host, monitored)
                 VALUES
                 {ParameterRows(chunk.Length, 5)}
                 ON CONFLICT (id) DO NOTHING
                 """,
                chunk.Select(endpoint => new object?[] { endpoint.Id, endpoint.Name, endpoint.HostId, endpoint.Host, endpoint.Monitored }),
                cancellationToken);
        }
    }

    public async Task ResolveRetriedMessages(ServiceControlDbContext dbContext, IReadOnlyList<ConfirmedRetry> rows, DateTime now, CancellationToken cancellationToken = default)
    {
        const int resolved = (int)FailedMessageStatus.Resolved;

        foreach (var chunk in rows.Chunk(MaxRowsPerStatement))
        {
            await Execute(
                dbContext,
                $"""
                 UPDATE failed_messages SET
                     status = {resolved},
                     status_changed_at = @p0,
                     last_modified = @p0
                 FROM (VALUES
                 {ConfirmedRetryRows(chunk.Length)}
                 ) AS s (unique_message_id, succeeded_at)
                 WHERE failed_messages.unique_message_id = s.unique_message_id
                   AND failed_messages.last_attempted_at <= s.succeeded_at
                 """,
                [[now], .. chunk.Select(retry => new object?[] { retry.UniqueMessageId, retry.SucceededAt })],
                cancellationToken);
        }
    }

    // A bare VALUES list has no target column to take its types from, so the first row carries them.
    static string ConfirmedRetryRows(int rowCount) =>
        string.Join(",\n", Enumerable.Range(0, rowCount).Select(row => row == 0
            ? "(@p1::uuid, @p2::timestamptz)"
            : $"(@p{1 + (row * 2)}, @p{2 + (row * 2)})"));

    // The columns the newer attempt wins wholesale
    static readonly string[] PayloadColumns =
    [
        "message_id", "message_type", "time_sent", "conversation_id", "failing_endpoint_address",
        "sending_endpoint_name", "sending_endpoint_host_id", "sending_endpoint_host",
        "receiving_endpoint_name", "receiving_endpoint_host_id", "receiving_endpoint_host",
        "exception_type", "exception_message", "is_system_message",
        "headers_json", "body_text", "body_stored_externally", "body_size", "body_content_type"
    ];

    // Column order matches FailedMessageValues
    static readonly string[] FailedMessageColumns =
    [
        "unique_message_id", "status", "status_changed_at", "last_modified",
        "number_of_processing_attempts", "first_time_of_failure", "last_time_of_failure", "last_attempted_at",
        .. PayloadColumns
    ];

    static object?[] FailedMessageValues(FailedMessageEntity row) =>
    [
        row.UniqueMessageId, (int)row.Status, row.StatusChangedAt, row.LastModified,
        row.NumberOfProcessingAttempts, row.FirstTimeOfFailure, row.LastTimeOfFailure, row.LastAttemptedAt,
        row.MessageId, row.MessageType, row.TimeSent, row.ConversationId, row.FailingEndpointAddress,
        row.SendingEndpointName, row.SendingEndpointHostId, row.SendingEndpointHost,
        row.ReceivingEndpointName, row.ReceivingEndpointHostId, row.ReceivingEndpointHost,
        row.ExceptionType, row.ExceptionMessage, row.IsSystemMessage,
        row.HeadersJson, row.BodyText, row.BodyStoredExternally, row.BodySize, row.BodyContentType
    ];

    static readonly string FailedMessageColumnList = string.Join(", ", FailedMessageColumns);

    static readonly string OnConflictUpdate = BuildOnConflictUpdate();

    // Strictly newer, where the payload columns take the incoming attempt on a tie as well. A
    // redelivery of the attempt already stored is not news, and must not undo a resolve or an
    // archive that happened after it.
    const string IsNewerAttempt = "excluded.last_attempted_at > failed_messages.last_attempted_at";

    static string BuildOnConflictUpdate()
    {
        const int unresolved = (int)FailedMessageStatus.Unresolved;

        var sql = new StringBuilder(
            $"""
             ON CONFLICT (unique_message_id) DO UPDATE SET
                 status = CASE WHEN {IsNewerAttempt} THEN {unresolved} ELSE failed_messages.status END,
                 status_changed_at = CASE WHEN {IsNewerAttempt} AND failed_messages.status <> {unresolved} THEN excluded.status_changed_at ELSE failed_messages.status_changed_at END,
                 last_modified = excluded.last_modified,
                 number_of_processing_attempts = failed_messages.number_of_processing_attempts
                     + CASE WHEN excluded.last_attempted_at <> failed_messages.last_attempted_at THEN excluded.number_of_processing_attempts ELSE 0 END,
                 first_time_of_failure = LEAST(failed_messages.first_time_of_failure, excluded.first_time_of_failure),
                 last_time_of_failure = GREATEST(failed_messages.last_time_of_failure, excluded.last_time_of_failure),
             """);

        foreach (var column in PayloadColumns)
        {
            sql.AppendLine().Append(
                $"    {column} = CASE WHEN excluded.last_attempted_at >= failed_messages.last_attempted_at THEN excluded.{column} ELSE failed_messages.{column} END,");
        }

        sql.AppendLine().Append("    last_attempted_at = GREATEST(failed_messages.last_attempted_at, excluded.last_attempted_at)");

        return sql.ToString();
    }
}
