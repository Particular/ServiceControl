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
    public Task RemoveFailedMessageRetry(string uniqueMessageId, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            if (Guid.TryParse(uniqueMessageId, out var id))
            {
                await dbContext.FailedMessageRetries
                    .Where(r => r.UniqueMessageId == id)
                    .ExecuteDeleteAsync(token);
            }
        }, cancellationToken);

    public Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var normalizedQueueAddress = queueAddress?.ToLowerInvariant();
            var ids = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(m => m.Status == FailedMessageStatus.RetryIssued
                            && m.LastModified >= from && m.LastModified <= to
                            && ((m.FailingEndpointAddress == null && normalizedQueueAddress == null)
                                || (m.FailingEndpointAddress != null && m.FailingEndpointAddress.ToLower() == normalizedQueueAddress)))
                .Select(m => m.UniqueMessageId.ToString())
                .ToListAsync(token);

            return ids.ToArray();
        }, cancellationToken);

    public Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, CancellationToken, Task> processCallback, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var query = dbContext.FailedMessages
                .AsNoTracking()
                .Where(m => m.Status == FailedMessageStatus.RetryIssued
                            && m.LastModified >= periodFrom && m.LastModified <= periodTo)
                .FilterByQueueAddress(queueAddress)
                .Select(m => m.UniqueMessageId.ToString());

            await foreach (var uniqueMessageId in query.AsAsyncEnumerable().WithCancellation(token))
            {
                await processCallback(uniqueMessageId, token);
            }
        }, cancellationToken);

    public async Task<byte[]> GetFailedMessageBody(string uniqueMessageId, CancellationToken cancellationToken = default)
    {
        var result = await bodyStorage.TryFetch(uniqueMessageId, cancellationToken)
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