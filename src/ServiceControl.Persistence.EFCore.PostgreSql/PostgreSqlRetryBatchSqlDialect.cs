namespace ServiceControl.Persistence.EFCore.PostgreSql;

using DbContexts;
using Entities;
using Infrastructure;

class PostgreSqlRetryBatchSqlDialect : PostgreSqlDialect, IRetryBatchSqlDialect
{
    public async Task InsertMissingRetryClaims(ServiceControlDbContext dbContext, IReadOnlyList<FailedMessageRetryEntity> rows, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in rows.Chunk(MaxRowsPerStatement))
        {
            await Execute(
                dbContext,
                $"""
                 INSERT INTO failed_message_retries (unique_message_id, retry_batch_id, stage_attempts)
                 VALUES
                 {ParameterRows(chunk.Length, 3)}
                 ON CONFLICT (unique_message_id) DO NOTHING
                 """,
                chunk.Select(retry => new object?[] { retry.UniqueMessageId, retry.RetryBatchId, retry.StageAttempts }),
                cancellationToken);
        }
    }
}
