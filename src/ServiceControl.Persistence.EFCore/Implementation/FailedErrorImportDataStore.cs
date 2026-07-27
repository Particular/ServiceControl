namespace ServiceControl.Persistence.EFCore.Implementation;

using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;
using ServiceControl.Persistence.EFCore.Infrastructure;

public class FailedErrorImportDataStore(
    IServiceScopeFactory scopeFactory,
    IBodyStoragePersistence bodyStorage,
    ILogger<FailedErrorImportDataStore> logger) : DataStoreBase(scopeFactory), IFailedErrorImportDataStore
{
    const int BatchSize = 100;

    public Task<bool> QueryContainsFailedImports() =>
        ExecuteWithDbContext(dbContext => dbContext.FailedErrorImports.AsNoTracking().AnyAsync());

    // Replays oldest-first. Successful imports delete their row; failures are left in place, so the
    // count of failures so far is exactly the offset to the next unseen row. This walks the whole
    // set once without retrying a failure within the same run.
    public async Task ProcessFailedErrorImports(Func<FailedTransportMessage, Task> processMessage, CancellationToken cancellationToken)
    {
        var succeeded = 0;
        var failed = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await ReadBatch(failed, cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var import in batch)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var transportMessage = await ToTransportMessage(import, cancellationToken);

                    await processMessage(transportMessage);

                    await DeleteImport(import, cancellationToken);

                    succeeded++;

                    logger.LogDebug("Successfully re-imported failed error message {MessageId}", import.MessageId);
                }
                catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation(e, "Cancelled");
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error while attempting to re-import failed error message {MessageId}", import.MessageId);
                    failed++;
                }
            }

            if (batch.Count < BatchSize)
            {
                break;
            }
        }

        logger.LogInformation("Done re-importing failed errors. Successfully re-imported {SucceededCount} messages. Failed re-importing {FailedCount} messages", succeeded, failed);

        if (failed > 0)
        {
            logger.LogWarning("{FailedCount} messages could not be re-imported. This could indicate a problem with the data. Contact Particular support if you need help with recovering the messages", failed);
        }
    }

    async Task<List<FailedErrorImportEntity>> ReadBatch(int offset, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        return await dbContext.FailedErrorImports
            .AsNoTracking()
            .OrderBy(import => import.FailedAt)
            .ThenBy(import => import.UniqueMessageId)
            .Skip(offset)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
    }

    async Task<FailedTransportMessage> ToTransportMessage(FailedErrorImportEntity import, CancellationToken cancellationToken)
    {
        var headers = JsonSerializer.Deserialize(import.HeadersJson, HeadersJsonContext.Default.DictionaryStringString) ?? [];

        var body = import.BodyStoredExternally
            ? await ReadExternalBody(import.UniqueMessageId, cancellationToken)
            : import.Body;

        return new FailedTransportMessage
        {
            Id = import.MessageId,
            Headers = headers,
            Body = body
        };
    }

    async Task<byte[]> ReadExternalBody(Guid uniqueMessageId, CancellationToken cancellationToken)
    {
        var stored = await bodyStorage.ReadBody(FailedErrorImportEntity.ExternalBodyId(uniqueMessageId), cancellationToken);

        if (stored is null)
        {
            return [];
        }

        await using var stream = stored.Stream;
        using var buffer = new MemoryStream(stored.BodySize);
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    // The row is removed before its external body: a surviving row with a missing body would replay
    // as an empty message, whereas an orphaned body is only a leak.
    async Task DeleteImport(FailedErrorImportEntity import, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        await dbContext.FailedErrorImports
            .Where(row => row.UniqueMessageId == import.UniqueMessageId)
            .ExecuteDeleteAsync(cancellationToken);

        if (import.BodyStoredExternally)
        {
            await DeleteExternalBody(import.UniqueMessageId, cancellationToken);
        }
    }

    async Task DeleteExternalBody(Guid uniqueMessageId, CancellationToken cancellationToken)
    {
        try
        {
            await bodyStorage.DeleteBody(FailedErrorImportEntity.ExternalBodyId(uniqueMessageId), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Re-import must not stall on a missing or unavailable body.
            logger.LogWarning(ex, "Could not delete the external body for re-imported failed error {UniqueMessageId}", uniqueMessageId);
        }
    }
}
