namespace ServiceControl.Persistence.EFCore.Implementation;

using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

// A body is stored inline in BodyText (small text) or in external storage (binary, or large text
// where BodyText keeps only a search prefix). External storage is authoritative, so check it first.
// bodyId is usually a UniqueMessageId (a Guid) but may be a plain MessageId.
public class BodyStorage(IServiceScopeFactory scopeFactory, IBodyStoragePersistence storagePersistence) : DataStoreBase(scopeFactory), IBodyStorage
{
    public async Task<MessageBodyStreamResult?> TryFetch(string bodyId)
    {
        var row = await ExecuteWithDbContext(dbContext => ResolveBody(dbContext, bodyId));

        if (row == null)
        {
            return null; // No such message: the API turns this into a 404.
        }

        // Bodies are immutable per message, so the id is a stable ETag.
        var uniqueMessageId = row.UniqueMessageId.ToString();

        if (row.BodyStoredExternally)
        {
            var external = await storagePersistence.ReadBody(uniqueMessageId);

            return external == null
                ? new MessageBodyStreamResult { HasResult = false }
                : new MessageBodyStreamResult
                {
                    HasResult = true,
                    Stream = external.Stream,
                    ContentType = external.ContentType,
                    BodySize = external.BodySize,
                    Etag = uniqueMessageId
                };
        }

        if (row.BodyText != null)
        {
            var bytes = Encoding.UTF8.GetBytes(row.BodyText);

            return new MessageBodyStreamResult
            {
                HasResult = true,
                Stream = new MemoryStream(bytes, writable: false),
                ContentType = row.BodyContentType,
                BodySize = bytes.Length,
                Etag = uniqueMessageId
            };
        }

        return new MessageBodyStreamResult { HasResult = false }; // Message exists but carries no body.
    }

    static async Task<BodyRow?> ResolveBody(ServiceControlDbContext dbContext, string bodyId)
    {
        if (Guid.TryParse(bodyId, out var uniqueMessageId))
        {
            var byUniqueId = await Query(dbContext, message => message.UniqueMessageId == uniqueMessageId);
            if (byUniqueId != null)
            {
                return byUniqueId;
            }
        }

        return await Query(dbContext, message => message.MessageId == bodyId);
    }

    static Task<BodyRow?> Query(ServiceControlDbContext dbContext, Expression<Func<FailedMessageEntity, bool>> predicate) =>
        dbContext.FailedMessages
            .AsNoTracking()
            .Where(predicate)
            .OrderBy(message => message.UniqueMessageId)
            .Select(message => new BodyRow
            {
                UniqueMessageId = message.UniqueMessageId,
                BodyText = message.BodyText,
                BodyStoredExternally = message.BodyStoredExternally,
                BodyContentType = message.BodyContentType
            })
            .FirstOrDefaultAsync();

    sealed class BodyRow
    {
        public Guid UniqueMessageId { get; init; }
        public string? BodyText { get; init; }
        public bool BodyStoredExternally { get; init; }
        public string? BodyContentType { get; init; }
    }
}
