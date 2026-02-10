namespace ServiceControl.Audit.Persistence.Sql.Core.Implementation;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Auditing.BodyStorage;
using ServiceControl.Audit.Persistence.Sql.Core.DbContexts;
using ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

class BodyStorageFetcher(IBodyStoragePersistence storagePersistence, AuditDbContextBase dbContext) : IBodyStorage
{
    public async Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken)
    {
        // Look up ProcessedAt from the database to locate the correct date folder
        var processedAt = await dbContext.ProcessedMessages
            .Where(m => m.UniqueMessageId == bodyId)
            .Select(m => m.ProcessedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (processedAt == default)
        {
            return new StreamResult { HasResult = false };
        }

        var result = await storagePersistence.ReadBodyAsync(bodyId, processedAt, cancellationToken);

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
