namespace ServiceControl.Persistence.EFCore.Implementation;

using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NServiceBus;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;
using ServiceControl.Persistence.EFCore.Infrastructure;

public class FailedErrorImportDataStore(
    IServiceScopeFactory scopeFactory,
    IBodyStoragePersistence bodyStorage,
    BodyStorageSettings bodyStorageSettings,
    TimeProvider timeProvider,
    ILogger<FailedErrorImportDataStore> logger) : DataStoreBase(scopeFactory), IFailedErrorImportDataStore
{
    const int BatchSize = 100;

    public Task<bool> QueryContainsFailedImports(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => dbContext.FailedErrorImports.AsNoTracking().AnyAsync(token), cancellationToken);

    // Update-first, then insert. The dedupe key is deterministic, so a repeat failure updates the
    // existing row and concurrent writers that both miss it race only on the insert. The loser of
    // that race confirms the row is now present (the winner stored the same logical failure) and
    // otherwise rethrows, so the caller never treats a message as stored when it is not.
    public Task StoreFailedErrorImport(FailedErrorImport failure, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var uniqueMessageId = FailedErrorImport.DeriveKey(failure.Message.Headers, failure.Message.Id);
            var body = failure.Message.Body ?? [];
            var storeExternally = body.Length > bodyStorageSettings.MaxBodySizeToStore;

            if (storeExternally)
            {
                var contentType = failure.Message.Headers.GetValueOrDefault(Headers.ContentType) ?? "application/octet-stream";
                await bodyStorage.WriteBody(FailedErrorImportEntity.ExternalBodyId(uniqueMessageId), body, contentType, token);
            }

            var failedAt = timeProvider.GetUtcNow().UtcDateTime;
            var headersJson = MessageHeaders.Write(failure.Message.Headers);
            byte[] storedBody = storeExternally ? [] : body;

            await dbContext.UpsertAsync([uniqueMessageId], () => new FailedErrorImportEntity
            {
                UniqueMessageId = uniqueMessageId,
                FailedAt = failedAt,
                MessageId = failure.Message.Id,
                HeadersJson = headersJson,
                Body = storedBody,
                BodyStoredExternally = storeExternally,
                ExceptionInfo = failure.ExceptionInfo
            }, (entity) =>
            {
                entity.FailedAt = failedAt;
                entity.MessageId = failure.Message.Id;
                entity.HeadersJson = headersJson;
                entity.Body = storedBody;
                entity.BodyStoredExternally = storeExternally;
                entity.ExceptionInfo = failure.ExceptionInfo;
            }, token);
        }, cancellationToken);

    // Replays oldest-first. Successful imports delete their row; failures are left in place, so the
    // count of failures so far is exactly the offset to the next unseen row. This walks the whole
    // set once without retrying a failure within the same run.
    public async Task ProcessFailedErrorImports(Func<FailedTransportMessage, CancellationToken, Task> processMessage, CancellationToken cancellationToken = default)
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

                    await processMessage(transportMessage, cancellationToken);

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
        var headers = MessageHeaders.Read(import.HeadersJson);

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
        var bodyId = FailedErrorImportEntity.ExternalBodyId(uniqueMessageId);
        var stored = await bodyStorage.ReadBody(bodyId, cancellationToken)
            ?? throw new InvalidOperationException($"The body for failed error import {uniqueMessageId} was not found in body storage under {bodyId}.");

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
#pragma warning disable PS0019 // The filter already excludes OperationCanceledException, so cancellation
        // propagates; PS0019 only recognises a cancellationToken.IsCancellationRequested guard.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Re-import must not stall on a missing or unavailable body.
            logger.LogWarning(ex, "Could not delete the external body for re-imported failed error {UniqueMessageId}", uniqueMessageId);
        }
#pragma warning restore PS0019
    }
}
