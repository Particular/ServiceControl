namespace ServiceControl.Persistence.EFCore.SqlServer;

using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

// MERGE WITH (HOLDLOCK) over an inline VALUES source. HOLDLOCK closes the race where two writers
// both miss a key and collide on the insert, and the single statement keeps every guard reading
// the same row state. Rows are chunked to stay clear of the 2100 parameter limit while keeping
// statement texts down to a few reusable shapes.
class SqlServerIngestionSqlDialect : IIngestionSqlDialect
{
    public async Task UpsertFailedMessages(ServiceControlDbContext dbContext, IReadOnlyList<FailedMessageEntity> rows, CancellationToken cancellationToken)
    {
        foreach (var chunk in rows.Chunk(MaxRowsPerStatement))
        {
            await Execute(
                dbContext,
                $"""
                 MERGE [FailedMessages] WITH (HOLDLOCK) AS t
                 USING (VALUES
                 {ParameterRows(chunk.Length, FailedMessageColumns.Length)}
                 ) AS s ({FailedMessageColumnList})
                 ON t.[UniqueMessageId] = s.[UniqueMessageId]
                 {WhenMatchedUpdate}
                 WHEN NOT MATCHED THEN INSERT ({FailedMessageColumnList})
                 VALUES ({FailedMessageSourceColumnList});
                 """,
                chunk.Select(FailedMessageValues),
                cancellationToken);
        }
    }

    public async Task InsertGroups(ServiceControlDbContext dbContext, IReadOnlyList<FailedMessageGroupEntity> rows, CancellationToken cancellationToken)
    {
        foreach (var chunk in rows.Chunk(MaxRowsPerStatement))
        {
            await Execute(
                dbContext,
                $"""
                 MERGE [FailedMessageGroups] WITH (HOLDLOCK) AS t
                 USING (VALUES
                 {ParameterRows(chunk.Length, 4)}
                 ) AS s ([FailedMessageUniqueId], [GroupId], [Title], [Type])
                 ON t.[FailedMessageUniqueId] = s.[FailedMessageUniqueId] AND t.[GroupId] = s.[GroupId]
                 WHEN NOT MATCHED THEN INSERT ([FailedMessageUniqueId], [GroupId], [Title], [Type])
                 VALUES (s.[FailedMessageUniqueId], s.[GroupId], s.[Title], s.[Type]);
                 """,
                chunk.Select(group => new object?[] { group.FailedMessageUniqueId, group.GroupId, group.Title, group.Type }),
                cancellationToken);
        }
    }

    public async Task InsertMissingKnownEndpoints(ServiceControlDbContext dbContext, IReadOnlyList<KnownEndpointEntity> rows, CancellationToken cancellationToken)
    {
        foreach (var chunk in rows.Chunk(MaxRowsPerStatement))
        {
            await Execute(
                dbContext,
                $"""
                 MERGE [KnownEndpoints] WITH (HOLDLOCK) AS t
                 USING (VALUES
                 {ParameterRows(chunk.Length, 5)}
                 ) AS s ([Id], [Name], [HostId], [Host], [Monitored])
                 ON t.[Id] = s.[Id]
                 WHEN NOT MATCHED THEN INSERT ([Id], [Name], [HostId], [Host], [Monitored])
                 VALUES (s.[Id], s.[Name], s.[HostId], s.[Host], s.[Monitored]);
                 """,
                chunk.Select(endpoint => new object?[] { endpoint.Id, endpoint.Name, endpoint.HostId, endpoint.Host, endpoint.Monitored }),
                cancellationToken);
        }
    }

    static async Task Execute(ServiceControlDbContext dbContext, string sql, IEnumerable<object?[]> rows, CancellationToken cancellationToken)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = (dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Ingestion statements must run inside the batch transaction")).GetDbTransaction();
        command.CommandText = sql;

        var index = 0;
        foreach (var row in rows)
        {
            foreach (var value in row)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@p{index++}";
                parameter.Value = value ?? DBNull.Value;

                // Attempt de-duplication compares LastAttemptedAt for equality, so datetime
                // parameters must keep datetime2 precision instead of the datetime default.
                if (value is DateTime)
                {
                    parameter.DbType = DbType.DateTime2;
                }

                command.Parameters.Add(parameter);
            }
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // The columns the newer attempt wins wholesale
    static readonly string[] PayloadColumns =
    [
        "MessageId", "MessageType", "TimeSent", "ConversationId", "QueueAddress",
        "SendingEndpointName", "SendingEndpointHostId", "SendingEndpointHost",
        "ReceivingEndpointName", "ReceivingEndpointHostId", "ReceivingEndpointHost",
        "ExceptionType", "ExceptionMessage", "IsSystemMessage",
        "HeadersJson", "BodyText", "BodyStoredExternally", "BodySize", "BodyContentType"
    ];

    // Column order matches FailedMessageValues
    static readonly string[] FailedMessageColumns =
    [
        "UniqueMessageId", "Status", "StatusChangedAt", "LastModified",
        "NumberOfProcessingAttempts", "FirstTimeOfFailure", "LastTimeOfFailure", "LastAttemptedAt",
        .. PayloadColumns
    ];

    static object?[] FailedMessageValues(FailedMessageEntity row) =>
    [
        row.UniqueMessageId, (int)row.Status, row.StatusChangedAt, row.LastModified,
        row.NumberOfProcessingAttempts, row.FirstTimeOfFailure, row.LastTimeOfFailure, row.LastAttemptedAt,
        row.MessageId, row.MessageType, row.TimeSent, row.ConversationId, row.QueueAddress,
        row.SendingEndpointName, row.SendingEndpointHostId, row.SendingEndpointHost,
        row.ReceivingEndpointName, row.ReceivingEndpointHostId, row.ReceivingEndpointHost,
        row.ExceptionType, row.ExceptionMessage, row.IsSystemMessage,
        row.HeadersJson, row.BodyText, row.BodyStoredExternally, row.BodySize, row.BodyContentType
    ];

    static readonly string FailedMessageColumnList = string.Join(", ", FailedMessageColumns.Select(column => $"[{column}]"));

    static readonly string FailedMessageSourceColumnList = string.Join(", ", FailedMessageColumns.Select(column => $"s.[{column}]"));

    static readonly string WhenMatchedUpdate = BuildWhenMatchedUpdate();

    static string BuildWhenMatchedUpdate()
    {
        const int unresolved = (int)FailedMessageStatus.Unresolved;

        var sql = new StringBuilder(
            $"""
             WHEN MATCHED THEN UPDATE SET
                 [Status] = {unresolved},
                 [StatusChangedAt] = CASE WHEN t.[Status] <> {unresolved} THEN s.[StatusChangedAt] ELSE t.[StatusChangedAt] END,
                 [LastModified] = s.[LastModified],
                 [NumberOfProcessingAttempts] = t.[NumberOfProcessingAttempts]
                     + CASE WHEN s.[LastAttemptedAt] <> t.[LastAttemptedAt] THEN s.[NumberOfProcessingAttempts] ELSE 0 END,
                 [FirstTimeOfFailure] = CASE WHEN s.[FirstTimeOfFailure] < t.[FirstTimeOfFailure] THEN s.[FirstTimeOfFailure] ELSE t.[FirstTimeOfFailure] END,
                 [LastTimeOfFailure] = CASE WHEN s.[LastTimeOfFailure] > t.[LastTimeOfFailure] THEN s.[LastTimeOfFailure] ELSE t.[LastTimeOfFailure] END,
             """);

        foreach (var column in PayloadColumns)
        {
            sql.AppendLine().Append(
                $"    [{column}] = CASE WHEN s.[LastAttemptedAt] >= t.[LastAttemptedAt] THEN s.[{column}] ELSE t.[{column}] END,");
        }

        sql.AppendLine().Append("    [LastAttemptedAt] = CASE WHEN s.[LastAttemptedAt] > t.[LastAttemptedAt] THEN s.[LastAttemptedAt] ELSE t.[LastAttemptedAt] END");

        return sql.ToString();
    }

    static string ParameterRows(int rowCount, int columnCount)
    {
        var sql = new StringBuilder();

        for (var row = 0; row < rowCount; row++)
        {
            sql.Append(row == 0 ? "(" : ",\n(");

            for (var column = 0; column < columnCount; column++)
            {
                if (column > 0)
                {
                    sql.Append(", ");
                }

                sql.Append("@p").Append((row * columnCount) + column);
            }

            sql.Append(')');
        }

        return sql.ToString();
    }

    // 27 columns * 50 rows stays well below the 2100 parameter limit
    const int MaxRowsPerStatement = 50;
}
