namespace ServiceControl.Audit.Persistence.Sql.Core.Implementation;

using ServiceControl.Audit.Auditing.BodyStorage;
using ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

class BodyStorageFetcher(IBodyStoragePersistence storagePersistence) : IBodyStorage
{
    public async Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken)
    {
        var result = await storagePersistence.ReadBodyAsync(bodyId, cancellationToken);

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
