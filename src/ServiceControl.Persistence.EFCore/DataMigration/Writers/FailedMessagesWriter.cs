namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using Abstractions;
using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes failed messages and their failure group rows, inserting only messages the target does not already hold
/// and never updating one it does. Each category that copies failed messages derives from this and names its
/// category id and the statuses it copies.
/// </summary>
abstract class FailedMessagesWriterBase(IMigrationSqlDialect migrationDialect, IBodyStoragePersistence storagePersistence, EFPersisterSettings persisterSettings) : MigrationCategoryWriter<FailedMessage>
{
    /// <summary>
    /// The statuses this category copies. <see cref="Count" /> counts only rows with one of these statuses, because
    /// both failed message categories share one table.
    /// </summary>
    protected abstract IReadOnlyCollection<FailedMessageStatus> Statuses { get; }

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatementFor<FailedMessageEntity>(dbContext);

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.FailedMessages
            .Where(message => Statuses.Contains(message.Status))
            .LongCountAsync(cancellationToken);

    protected override async Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, FailedMessage Document)> documents, CancellationToken cancellationToken = default)
    {
        var messageInsert = MigrationInsert<FailedMessageEntity>.For(dbContext);
        var groupInsert = MigrationInsert<FailedMessageGroupEntity>.For(dbContext);
        List<(string SourceId, MigrationSkipReason Reason, string Detail)> skips = [];
        List<FailedMessageRowMapping.Mapped> mapped = [];
        HashSet<Guid> mappedIds = [];
        List<(string SourceId, FailedMessageEntity Row, MigrationBody Body)> externalBodies = [];

        foreach (var (row, document) in documents)
        {
            switch (NamingTheDocument(row, () => FailedMessageRowMapper.Map(row)))
            {
                case FailedMessageRowMapping.Mapped mapping:
                    var body = row.Body ?? throw new InvalidOperationException($"Row '{row.SourceId}' in category {CategoryId} reached the writer without a body. The engine attaches a body to every row of a category that carries bodies, or skips the row.");
                    // Set before the length check, because the stored content type is a text column with a limit.
                    var storeExternally = NamingTheDocument(row, () => FailedMessageRowMapper.SetBody(mapping.Row, document.ProcessingAttempts[^1].Headers, body, persisterSettings.BodyStorage.MaxBodySizeToStore));

                    // A message copied without one of its groups would be missing from that group, so a group that cannot be stored skips the message.
                    var unstorable = UnstorableValueSkip(messageInsert, migrationDialect, row, mapping.Row)
                        ?? mapping.Groups.Select(group => UnstorableValueSkip(groupInsert, migrationDialect, row, group)).FirstOrDefault(groupSkip => groupSkip is not null);

                    if (unstorable is { } unstorableSkip)
                    {
                        skips.Add(unstorableSkip);
                    }
                    else
                    {
                        mapped.Add(mapping);

                        // The insert keeps only the first document with an id, so only its body may be stored under that id.
                        if (mappedIds.Add(mapping.Row.UniqueMessageId) && storeExternally)
                        {
                            externalBodies.Add((row.SourceId, mapping.Row, body));
                        }
                    }

                    break;
                case FailedMessageRowMapping.Skipped skip:
                    skips.Add((row.SourceId, skip.Reason, skip.Detail));
                    break;
                default:
                    throw new InvalidOperationException($"The mapper returned a mapping the writer does not know for document '{row.SourceId}'.");
            }
        }

        await WriteBodiesTheTargetLacks(dbContext, externalBodies, cancellationToken);

        return new PreparedBatch((context, token) => InsertFailedMessages(context, mapped, token), mapped.Count, skips, Merges: []);
    }

    // All bodies are written before the transaction opens, so a crash leaves a body with no row rather than a row pointing at a body that was never stored.
    async Task WriteBodiesTheTargetLacks(ServiceControlDbContext dbContext, IReadOnlyList<(string SourceId, FailedMessageEntity Row, MigrationBody Body)> externalBodies, CancellationToken cancellationToken)
    {
        if (externalBodies.Count == 0)
        {
            return;
        }

        var ids = externalBodies.Select(external => external.Row.UniqueMessageId).ToArray();

        // This only skips writing bodies for messages the target already holds; InsertMissing still decides which rows go in.
        var present = (await dbContext.FailedMessages
            .AsNoTracking()
            .Where(message => ids.Contains(message.UniqueMessageId))
            .Select(message => message.UniqueMessageId)
            .ToListAsync(cancellationToken)).ToHashSet();

        await Task.WhenAll(externalBodies
            .Where(external => !present.Contains(external.Row.UniqueMessageId))
            .Select(external => WriteBody(external, cancellationToken)));
    }

    async Task WriteBody((string SourceId, FailedMessageEntity Row, MigrationBody Body) external, CancellationToken cancellationToken)
    {
        var bodyId = external.Row.UniqueMessageId.ToString();

        try
        {
            await storagePersistence.WriteBody(bodyId, external.Body.Content, external.Body.ContentType, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // The storage SDK errors do not name the body, so without this the error that stops the copy would not say which message failed.
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Writing the body of document '{external.SourceId}' in category {CategoryId} to external body storage as '{bodyId}' ({external.Body.Content.Length} bytes) failed with {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    // Adds the document id to the error, because the bare error names no document and the source can hold millions.
    T NamingTheDocument<T>(MigrationRow row, Func<T> step)
    {
        try
        {
            return step();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Preparing document '{row.SourceId}' in category {CategoryId} failed with {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    async Task<int> InsertFailedMessages(ServiceControlDbContext dbContext, IReadOnlyList<FailedMessageRowMapping.Mapped> mapped, CancellationToken cancellationToken)
    {
        var inserted = (await migrationDialect.InsertMissing(dbContext, [.. mapped.Select(mapping => mapping.Row)], cancellationToken))
            .ToHashSet<FailedMessageEntity>(ReferenceEqualityComparer.Instance);

        // Matched by reference, because InsertMissing returns the row objects it kept, and a document it turned away can share a kept row's id.
        // A group goes in only when the message its foreign key points at went in, so a message already in the target keeps the groups it had.
        await migrationDialect.InsertMissing(dbContext, [.. mapped.Where(mapping => inserted.Contains(mapping.Row)).SelectMany(mapping => mapping.Groups)], cancellationToken);

        return inserted.Count;
    }
}

/// <summary>
/// Writes the unresolved and retry-issued failed messages.
/// </summary>
sealed class FailedMessagesWriter(IMigrationSqlDialect migrationDialect, IBodyStoragePersistence storagePersistence, EFPersisterSettings persisterSettings)
    : FailedMessagesWriterBase(migrationDialect, storagePersistence, persisterSettings)
{
    public override string CategoryId => MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages;

    protected override IReadOnlyCollection<FailedMessageStatus> Statuses => MigrationCategoryRegistry.UnresolvedAndRetryIssuedStatuses;
}
