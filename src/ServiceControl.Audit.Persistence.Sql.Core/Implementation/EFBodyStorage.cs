namespace ServiceControl.Audit.Persistence.Sql.Core.Implementation;

using Infrastructure;
using ServiceControl.Audit.Auditing.BodyStorage;

class EFBodyStorage(FileSystemBodyStorageHelper helper) : IBodyStorage
{
    public async Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await bodyStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        await helper.WriteBodyAsync(bodyId, ms.ToArray(), contentType, cancellationToken).ConfigureAwait(false);
    }

    public Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken)
        => Task.FromResult(new StreamResult { HasResult = false }); // No-op for initial test
}
