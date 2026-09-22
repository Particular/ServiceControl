namespace ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.Audit;
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
public class BodyStorage(IServiceScopeFactory scopeFactory, IBodyStoragePersistence storagePersistence, EFPersisterSettings settings) : DataStoreBase(scopeFactory), IBodyStorage
{
    public async Task<MessageBodyResult> TryFetch(string bodyId, CancellationToken cancellationToken = default)
    {
        var row = await ExecuteWithDbContext((dbContext, token) => ResolveBody(dbContext, bodyId, settings.HostsAuditData, token), cancellationToken);

        if (row == null)
        {
            return MessageBodyResult.NotFound();
        }

        // Ingestion updates the existing row rather than adding one, so the message id is unchanged
        // and cannot serve as a version alone. LastModified is written on every upsert.
        var version = DataVersion.Compose(
            ("uniqueMessageId", row.UniqueMessageId),
            ("lastModified", row.LastModified));

        if (row.BodyStoredExternally)
        {
            var external = await storagePersistence.ReadBody(row.ExternalBodyId, cancellationToken);

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

    static async Task<BodyRow?> ResolveBody(ServiceControlDbContext dbContext, string bodyId, bool hostsAuditData, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(bodyId, out var uniqueMessageId))
        {
            var byUniqueId = await Query(dbContext, message => message.UniqueMessageId == uniqueMessageId, cancellationToken);
            if (byUniqueId != null)
            {
                return byUniqueId;
            }
        }

        var byMessageId = await Query(dbContext, message => message.MessageId == bodyId, cancellationToken);
        if (byMessageId != null)
        {
            return byMessageId;
        }

        return hostsAuditData && Guid.TryParse(bodyId, out var auditUniqueMessageId)
            ? await QueryAudit(dbContext, auditUniqueMessageId, cancellationToken)
            : null;
    }

    // The newest row wins when a redelivered message left more than one, since none of them differ.
    static Task<BodyRow?> QueryAudit(ServiceControlDbContext dbContext, Guid uniqueMessageId, CancellationToken cancellationToken) =>
        dbContext.AuditMessages
            .AsNoTracking()
            .Where(message => message.UniqueMessageId == uniqueMessageId)
            .OrderByDescending(message => message.CreatedOn)
            .ThenByDescending(message => message.Id)
            .Select(message => new BodyRow
            {
                UniqueMessageId = message.UniqueMessageId,
                IngestionHour = message.CreatedOn,
                BodyText = message.BodyText,
                BodyStoredExternally = message.BodyStoredExternally,
                BodySize = message.BodySize,
                BodyContentType = message.BodyContentType,
                LastModified = message.CreatedOn
            })
            .FirstOrDefaultAsync(cancellationToken);

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
        public DateTime? IngestionHour { get; init; }
        public string? BodyText { get; init; }

        // Composed here rather than in the query: a Guid rendered by SQL Server is upper case, and
        // the stored key is the lower case string the ingestion wrote.
        public string ExternalBodyId => IngestionHour is { } hour
            ? AuditBodyStorage.BodyId(hour, UniqueMessageId)
            : UniqueMessageId.ToString();
        public bool BodyStoredExternally { get; init; }
        public int BodySize { get; init; }
        public string? BodyContentType { get; init; }
        public DateTime LastModified { get; init; }
    }
}
