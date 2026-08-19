namespace ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;

/// <summary>
/// Resolves a message body from wherever it was stored.
/// </summary>
/// <remarks>
/// A body is stored inline in BodyText (small text) or in external storage (binary, or large text
/// where BodyText keeps only a search prefix). External storage is authoritative, so check it first.
/// bodyId is usually a UniqueMessageId (a Guid) but may be a plain MessageId.
/// </remarks>
public class BodyStorage(IServiceScopeFactory scopeFactory, IBodyStoragePersistence storagePersistence) : DataStoreBase(scopeFactory), IBodyStorage
{
    public async Task<MessageBodyResult> TryFetch(string bodyId, CancellationToken cancellationToken = default)
    {
        var row = await ExecuteWithDbContext((dbContext, token) => ResolveBody(dbContext, bodyId, token), cancellationToken);

        if (row == null)
        {
            return MessageBodyResult.NotFound();
        }

        var uniqueMessageId = row.UniqueMessageId.ToString();

        // Ingestion updates the existing row rather than adding one, so the message id is unchanged
        // and cannot serve as a version alone. LastModified is written on every upsert.
        var version = DataVersion.Compose(
            ("uniqueMessageId", row.UniqueMessageId),
            ("lastModified", row.LastModified));

        if (row.BodyStoredExternally)
        {
            var external = await storagePersistence.ReadBody(uniqueMessageId, cancellationToken);

            if (external == null)
            {
                return MessageBodyResult.Unavailable();
            }

            if (external.BodySize == 0)
            {
                await external.Stream.DisposeAsync();
                return MessageBodyResult.Empty();
            }

            return MessageBodyResult.Available(new MessageBodyStreamContent(external.Stream, external.ContentType, external.BodySize, version));
        }

        if (row.BodyText != null)
        {
            var bytes = Encoding.UTF8.GetBytes(row.BodyText);

            if (bytes.Length == 0)
            {
                return MessageBodyResult.Empty();
            }

            return MessageBodyResult.Available(new MessageBodyStreamContent(
                new MemoryStream(bytes, writable: false),
                row.BodyContentType ?? "text/plain",
                bytes.Length,
                version));
        }

        if (row.BodySize == 0)
        {
            return MessageBodyResult.Empty();
        }

        return MessageBodyResult.Unavailable();
    }

    static async Task<BodyRow?> ResolveBody(ServiceControlDbContext dbContext, string bodyId, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(bodyId, out var uniqueMessageId))
        {
            var byUniqueId = await Query(dbContext, message => message.UniqueMessageId == uniqueMessageId, cancellationToken);
            if (byUniqueId != null)
            {
                return byUniqueId;
            }
        }

        return await Query(dbContext, message => message.MessageId == bodyId, cancellationToken);
    }

    static Task<BodyRow?> Query(ServiceControlDbContext dbContext, Expression<Func<FailedMessageEntity, bool>> predicate, CancellationToken cancellationToken) =>
        dbContext.FailedMessages
            .AsNoTracking()
            .Where(predicate)
            .OrderBy(message => message.UniqueMessageId)
            .Select(message => new BodyRow
            {
                UniqueMessageId = message.UniqueMessageId,
                BodyText = message.BodyText,
                BodyStoredExternally = message.BodyStoredExternally,
                BodySize = message.BodySize,
                BodyContentType = message.BodyContentType,
                LastModified = message.LastModified
            })
            .FirstOrDefaultAsync(cancellationToken);

    sealed class BodyRow
    {
        public Guid UniqueMessageId { get; init; }
        public string? BodyText { get; init; }
        public bool BodyStoredExternally { get; init; }
        public int BodySize { get; init; }
        public string? BodyContentType { get; init; }
        public DateTime LastModified { get; init; }
    }
}
