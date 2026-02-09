namespace ServiceControl.Audit.Persistence.Sql.Core.Implementation;

using DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Audit.Auditing.BodyStorage;
using ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

class BodyStorageFetcher(IBodyStoragePersistence storagePersistence, IServiceScopeFactory serviceScopeFactory) : IBodyStorage
{
    public async Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken)
    {
        // Look up the BatchId from the database
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContextBase>();

        var batchId = await dbContext.ProcessedMessages
            .Where(m => m.UniqueMessageId == bodyId)
            .Select(m => m.BatchId)
            .FirstOrDefaultAsync(cancellationToken);

        if (batchId == Guid.Empty)
        {
            return new StreamResult { HasResult = false };
        }

        var result = await storagePersistence.ReadBodyAsync(bodyId, batchId, cancellationToken);

        if (result == null)
        {
            return new StreamResult { HasResult = false };
        }

        return new StreamResult
        {
            HasResult = true,
            Stream = result.Stream,
            ContentType = result.ContentType,
            BodySize = result.BodySize,
            Etag = result.Etag
        };
    }
}
