namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using Abstractions;
using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes failed messages, their failure group rows and a retry claim for each retry-issued message the source still
/// held one for, inserting only messages the target does not already hold and never updating one it does. The claims
/// mirror the source, so a retry by id behaves as it did before the move. A retry-issued row with no bool RetryClaimHeld
/// value fails the batch. Each category that copies failed messages derives from this and names its category id and the
/// statuses it copies.
/// </summary>
abstract class FailedMessagesWriterBase(IMigrationSqlDialect migrationDialect, IBodyStoragePersistence storagePersistence, EFPersisterSettings persisterSettings) : MigrationCategoryWriter<FailedMessage>
{
    // Like the claim a finished retry batch leaves, this points at no batch, so nothing stages it until it is released by message id.
    public static readonly Guid MigratedRetryClaimBatchId = new("5c1b7a0e-2f4d-4e8a-9b36-7d0c4f1e8a52");

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
        List<FailedMessageDocumentTranslation.Translated> translated = [];
        HashSet<Guid> translatedIds = [];
        List<(string SourceId, FailedMessageEntity Row, MigrationBody Body)> externalBodies = [];
        List<(string SourceId, Guid UniqueMessageId, string Detail)> changes = [];
        HashSet<Guid> heldClaims = [];
        var groupIds = migrationDialect.KeyComparer(typeof(FailedMessageGroupEntity), nameof(FailedMessageGroupEntity.GroupId));

        foreach (var (row, document) in documents)
        {
            switch (NamingTheDocument(row, () => FailedMessageDocumentTranslator.Translate(row)))
            {
                case FailedMessageDocumentTranslation.Translated translation:
                    var body = row.Body ?? throw new InvalidOperationException($"Row '{row.SourceId}' in category {CategoryId} reached the writer without a body. The engine attaches a body to every row of a category that carries bodies, or skips the row.");
                    // Set before the length check, because the stored content type is a text column with a limit.
                    var storeExternally = NamingTheDocument(row, () => FailedMessageDocumentTranslator.SetBody(translation.Row, document.ProcessingAttempts[^1].Headers, body, persisterSettings.BodyStorage.MaxBodySizeToStore));

                    // A message copied without one of its groups would be missing from that group, so a group that cannot be stored skips the message.
                    var unstorable = UnstorableValueSkip(messageInsert, migrationDialect, row, translation.Row)
                        ?? translation.Groups.Select(group => UnstorableValueSkip(groupInsert, migrationDialect, row, group)).FirstOrDefault(groupSkip => groupSkip is not null);

                    if (unstorable is { } unstorableSkip)
                    {
                        skips.Add(unstorableSkip);
                    }
                    else
                    {
                        translated.Add(translation);

                        // The insert keeps only the first document with an id, so only its body is stored under that id, only its changes are reported and only its claim is copied.
                        if (translatedIds.Add(translation.Row.UniqueMessageId))
                        {
                            if (storeExternally)
                            {
                                externalBodies.Add((row.SourceId, translation.Row, body));
                            }

                            if (translation.Row.Status == FailedMessageStatus.RetryIssued && SourceHeldRetryClaim(row))
                            {
                                heldClaims.Add(translation.Row.UniqueMessageId);
                            }

                            if (ChangesIn(translation, groupIds) is { Count: > 0 } changed)
                            {
                                changes.Add((row.SourceId, translation.Row.UniqueMessageId, string.Join(" ", changed)));
                            }
                        }
                    }

                    break;
                case FailedMessageDocumentTranslation.Skipped skip:
                    skips.Add((row.SourceId, skip.Reason, skip.Detail));
                    break;
                default:
                    throw new InvalidOperationException($"The translator returned a translation the writer does not know for document '{row.SourceId}'.");
            }
        }

        // This only skips the bodies and changes of messages the target already holds; InsertMissing still decides which rows go in.
        var present = externalBodies.Count == 0 && changes.Count == 0
            ? []
            : await MessagesTheTargetHolds(dbContext, [.. externalBodies.Select(external => external.Row.UniqueMessageId), .. changes.Select(change => change.UniqueMessageId)], cancellationToken);

        // All bodies are written before the transaction opens, so a crash leaves a body with no row rather than a row pointing at a body that was never stored.
        await Task.WhenAll(externalBodies
            .Where(external => !present.Contains(external.Row.UniqueMessageId))
            .Select(external => WriteBody(external, cancellationToken)));

        return new PreparedBatch((context, token) => InsertFailedMessages(context, translated, heldClaims, token), translated.Count, skips, Merges: [])
        {
            Changes = [.. changes.Where(change => !present.Contains(change.UniqueMessageId)).Select(change => (change.SourceId, change.Detail))]
        };
    }

    static async Task<HashSet<Guid>> MessagesTheTargetHolds(ServiceControlDbContext dbContext, Guid[] ids, CancellationToken cancellationToken) =>
        (await dbContext.FailedMessages
            .AsNoTracking()
            .Where(message => ids.Contains(message.UniqueMessageId))
            .Select(message => message.UniqueMessageId)
            .ToListAsync(cancellationToken)).ToHashSet();

    // The group insert keeps the first of the ids its key treats as one, so a later group with such an id loses its title and type.
    static IReadOnlyList<string> ChangesIn(FailedMessageDocumentTranslation.Translated translation, IEqualityComparer<string> groupIds) =>
    [
        .. translation.Changes,
        .. translation.Groups
            .GroupBy(group => group.GroupId, groupIds)
            .Where(sameKey => sameKey.Count() > 1)
            .Select(sameKey => $"Kept the failure group '{sameKey.First().GroupId}' titled '{sameKey.First().Title}' and dropped {string.Join(", ", sameKey.Skip(1).Select(group => $"'{group.GroupId}' titled '{group.Title}'"))}, because SQL treats their ids as one key.")
    ];

    // Throws rather than guess, because a wrong guess either blocks a retry by id or lets one send the message twice.
    static bool SourceHeldRetryClaim(MigrationRow row) =>
        row.Metadata.GetValueOrDefault(MigrationMetadataKeys.RetryClaimHeld) switch
        {
            bool held => held,
            null => throw new InvalidOperationException($"Row '{row.SourceId}' is retry-issued but arrived without a {MigrationMetadataKeys.RetryClaimHeld} value, which the source resolves for every retry-issued failed message"),
            var other => throw new InvalidOperationException($"Row '{row.SourceId}' carries a {other.GetType().FullName} as its {MigrationMetadataKeys.RetryClaimHeld} value, where the source sets a {typeof(bool).FullName}")
        };

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

    async Task<int> InsertFailedMessages(ServiceControlDbContext dbContext, IReadOnlyList<FailedMessageDocumentTranslation.Translated> translated, IReadOnlySet<Guid> heldClaims, CancellationToken cancellationToken)
    {
        var inserted = (await migrationDialect.InsertMissing(dbContext, [.. translated.Select(translation => translation.Row)], cancellationToken))
            .ToHashSet<FailedMessageEntity>(ReferenceEqualityComparer.Instance);

        // Matched by reference, because InsertMissing returns the row objects it kept, and a document it turned away can share a kept row's id.
        // A group goes in only when the message its foreign key points at went in, so a message already in the target keeps the groups it had.
        await migrationDialect.InsertMissing(dbContext, [.. translated.Where(translation => inserted.Contains(translation.Row)).SelectMany(translation => translation.Groups)], cancellationToken);

        // The claims mirror the source, so a retry by id stays blocked or goes through exactly as it would have before the move.
        await migrationDialect.InsertMissing(dbContext, [.. inserted
            .Where(row => heldClaims.Contains(row.UniqueMessageId))
            .Select(row => new FailedMessageRetryEntity { UniqueMessageId = row.UniqueMessageId, RetryBatchId = MigratedRetryClaimBatchId })], cancellationToken);

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
