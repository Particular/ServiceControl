namespace ServiceControl.Persistence.EFCore.Implementation;

using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence.EFCore.Infrastructure;

public class FailedMessageRetryDataStore(IServiceScopeFactory scopeFactory, IBodyStorage bodyStorage)
    : DataStoreBase(scopeFactory), IFailedMessageRetryDataStore
{
    public Task RemoveFailedMessageRetry(string uniqueMessageId, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(async dbContext =>
        {
            if (!Guid.TryParse(uniqueMessageId, out var id))
            {
                return;
            }

            // Point delete by primary key. ExecuteDeleteAsync returns the affected row count
            // (0 when absent), so this is idempotent and never throws on a missing row — matching
            // the RavenDB behaviour where a DeleteDocumentCommand on a non-existent doc is a no-op.
            await dbContext.FailedMessageRetries
                .Where(r => r.UniqueMessageId == id)
                .ExecuteDeleteAsync(cancellationToken: cancellationToken);
        });

    public Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(async dbContext =>
        {
            // Pending retries are FailedMessages whose status is RetryIssued and whose LastModified
            // falls within [from, to]. The RetryStagingStore.MarkBatchAsForwarding sets both
            // LastModified and StatusChangedAt to "now" when a retry is issued, so LastModified
            // reflects when the retry was sent — matching the RavenDB reference, which filters on
            // LastModified for both pending-retry queries.
            var normalizedQueueAddress = queueAddress?.ToLowerInvariant();
            var ids = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(m => m.Status == FailedMessageStatus.RetryIssued
                    && m.LastModified >= from && m.LastModified <= to
                    && ((m.FailingEndpointAddress == null && normalizedQueueAddress == null)
                        || (m.FailingEndpointAddress != null && m.FailingEndpointAddress.ToLower() == normalizedQueueAddress)))
                .Select(m => m.UniqueMessageId.ToString())
                .ToListAsync(cancellationToken: cancellationToken);

            return ids.ToArray();
        });

    public Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, CancellationToken, Task> processCallback, CancellationToken cancellationToken) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.FailedMessages
                .AsNoTracking()
                .Where(m => m.Status == FailedMessageStatus.RetryIssued
                            && m.LastModified >= periodFrom && m.LastModified <= periodTo)
                .FilterByQueueAddress(queueAddress)
                .Select(m => m.UniqueMessageId.ToString());

            await foreach (var uniqueMessageId in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                await processCallback(uniqueMessageId, cancellationToken);
            }
        });

    public async Task<byte[]> GetFailedMessageBody(string uniqueMessageId, CancellationToken cancellationToken)
    {
        var result = await bodyStorage.TryFetch(uniqueMessageId)
                     ?? throw new InvalidOperationException("IBodyStorage.TryFetch result cannot be null");

        if (!result.HasResult)
        {
            throw new InvalidOperationException("IBodyStorage.TryFetch did not return a body");
        }

        await using (result.Stream)
        {
            using var memoryStream = new MemoryStream();
            await result.Stream.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }
    }
}