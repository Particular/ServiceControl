namespace ServiceControl.Persistence.EFCore.SqlServer;

using DbContexts;
using Entities;
using Infrastructure;

class SqlServerRetryBatchSqlDialect : SqlServerDialect, IRetryBatchSqlDialect
{
    public async Task InsertMissingRetryClaims(ServiceControlDbContext dbContext, IReadOnlyList<FailedMessageRetryEntity> rows, CancellationToken cancellationToken = default)
    {
        var maxRowsPerStatement = MaxRowsPerStatement(3);
        foreach (var chunk in rows.Chunk(maxRowsPerStatement))
        {
            await Execute(
                dbContext,
                $"""
                 MERGE [FailedMessageRetries] WITH (HOLDLOCK) AS t
                 USING (VALUES
                 {ParameterRows(chunk.Length, 3)}
                 ) AS s ([UniqueMessageId], [RetryBatchId], [StageAttempts])
                 ON t.[UniqueMessageId] = s.[UniqueMessageId]
                 WHEN NOT MATCHED THEN INSERT ([UniqueMessageId], [RetryBatchId], [StageAttempts])
                 VALUES (s.[UniqueMessageId], s.[RetryBatchId], s.[StageAttempts]);
                 """,
                chunk.Select(retry => new object?[] { retry.UniqueMessageId, retry.RetryBatchId, retry.StageAttempts }),
                cancellationToken);
        }
    }
}
