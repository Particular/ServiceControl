namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NServiceBus;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

// The audit counterpart of FailedErrorImportDataStore, keyed by FailedAuditImport.DeriveKey so
// that a poison message produces one row however many workers attempt it.
public class FailedAuditImportDataStore(
    IServiceScopeFactory scopeFactory,
    IBodyStoragePersistence bodyStorage,
    BodyStorageSettings bodyStorageSettings,
    TimeProvider timeProvider,
    ILogger<FailedAuditImportDataStore> logger) : DataStoreBase(scopeFactory), IFailedAuditImportDataStore
{
    const int BatchSize = 100;

    public Task<bool> QueryContainsFailedImports(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => dbContext.FailedAuditImports.AsNoTracking().AnyAsync(token), cancellationToken);

    // Update-first, then insert. The dedupe key is deterministic, so a repeat failure updates the
    // existing row and concurrent writers that both miss it race only on the insert.
    public Task StoreFailedAuditImport(FailedAuditImport failure, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var uniqueMessageId = FailedAuditImport.DeriveKey(failure.Message!.Headers, failure.Message.Id);
            var body = failure.Message.Body ?? [];
            var storeExternally = body.Length > bodyStorageSettings.MaxBodySizeToStore;

            if (storeExternally)
            {
                var contentType = failure.Message.Headers.GetValueOrDefault(Headers.ContentType) ?? "application/octet-stream";
                await bodyStorage.WriteBody(FailedAuditImportEntity.ExternalBodyId(uniqueMessageId), body, contentType, token);
            }

            var failedAt = timeProvider.GetUtcNow().UtcDateTime;
            var headersJson = MessageHeaders.Write(failure.Message.Headers);
            byte[] storedBody = storeExternally ? [] : body;

            await dbContext.UpsertAsync([uniqueMessageId], () => new FailedAuditImportEntity
            {
                UniqueMessageId = uniqueMessageId,
                FailedAt = failedAt,
                MessageId = failure.Message.Id,
                HeadersJson = headersJson,
                Body = storedBody,
                BodyStoredExternally = storeExternally,
                ExceptionInfo = failure.ExceptionInfo ?? string.Empty
            }, entity =>
            {
                entity.FailedAt = failedAt;
                entity.MessageId = failure.Message.Id;
                entity.HeadersJson = headersJson;
                entity.Body = storedBody;
                entity.BodyStoredExternally = storeExternally;
                entity.ExceptionInfo = failure.ExceptionInfo ?? string.Empty;
            }, token);
        }, cancellationToken);

    // Replays oldest-first. Successful imports delete their row; failures are left in place, so the
    // count of failures so far is exactly the offset to the next unseen row.
    public async Task ProcessFailedAuditImports(Func<FailedTransportMessage, CancellationToken, Task> processMessage, CancellationToken cancellationToken = default)
    {
        var succeeded = 0;
        var failed = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = await ReadBatch(failed, cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var import in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var transportMessage = await ToTransportMessage(import, cancellationToken);

                    await processMessage(transportMessage, cancellationToken);

                    await DeleteImport(import, cancellationToken);

                    succeeded++;

                    logger.LogDebug("Successfully re-imported failed audit message {MessageId}", import.MessageId);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error while attempting to re-import failed audit message {MessageId}", import.MessageId);
                    failed++;
                }
            }

            if (batch.Count < BatchSize)
            {
                break;
            }
        }

        logger.LogInformation("Done re-importing failed audits. Successfully re-imported {SucceededCount} messages. Failed re-importing {FailedCount} messages", succeeded, failed);

        if (failed > 0)
        {
            logger.LogWarning("{FailedCount} messages could not be re-imported. This could indicate a problem with the data. Contact Particular support if you need help with recovering the messages", failed);
        }
    }

    async Task<List<FailedAuditImportEntity>> ReadBatch(int offset, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        return await dbContext.FailedAuditImports
            .AsNoTracking()
            .OrderBy(import => import.FailedAt)
            .ThenBy(import => import.UniqueMessageId)
            .Skip(offset)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
    }

    async Task<FailedTransportMessage> ToTransportMessage(FailedAuditImportEntity import, CancellationToken cancellationToken)
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
        var bodyId = FailedAuditImportEntity.ExternalBodyId(uniqueMessageId);
        var stored = await bodyStorage.ReadBody(bodyId, cancellationToken)
            ?? throw new InvalidOperationException($"The body for failed audit import {uniqueMessageId} was not found in body storage under {bodyId}.");

        await using var stream = stored.Stream;
        using var buffer = new MemoryStream(stored.BodySize);
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    // The row is removed before its external body: a surviving row with a missing body would replay
    // as an empty message, whereas an orphaned body is only a leak.
    async Task DeleteImport(FailedAuditImportEntity import, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        await dbContext.FailedAuditImports
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
            await bodyStorage.DeleteBodyIfExists(FailedAuditImportEntity.ExternalBodyId(uniqueMessageId), cancellationToken);
        }
#pragma warning disable PS0019 // The filter already excludes OperationCanceledException, so cancellation
        // propagates; PS0019 only recognises a cancellationToken.IsCancellationRequested guard.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Re-import must not stall on a missing or unavailable body.
            logger.LogWarning(ex, "Could not delete the external body for re-imported failed audit {UniqueMessageId}", uniqueMessageId);
        }
#pragma warning restore PS0019
    }
}
