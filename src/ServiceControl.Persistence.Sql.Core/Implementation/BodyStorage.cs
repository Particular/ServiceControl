namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Operations.BodyStorage;

public class BodyStorage : DataStoreBase, IBodyStorage
{
    readonly FileSystemBodyStorageHelper storageHelper;

    public BodyStorage(IServiceScopeFactory scopeFactory, FileSystemBodyStorageHelper storageHelper) : base(scopeFactory)
    {
        this.storageHelper = storageHelper;
    }
    public async Task<MessageBodyStreamResult> TryFetch(string bodyId)
    {
        try
        {
            var result = await storageHelper.ReadBodyAsync(bodyId);

            if (result == null)
            {
                return new MessageBodyStreamResult { HasResult = false };
            }

            return new MessageBodyStreamResult
            {
                HasResult = true,
                Stream = result.Stream, // Already positioned, decompression handled
                ContentType = result.ContentType,
                BodySize = result.BodySize,
                Etag = result.Etag
            };
        }
        catch
        {
            return new MessageBodyStreamResult { HasResult = false };
        }
    }
}
