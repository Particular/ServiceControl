#nullable enable
namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client;
using Raven.Client.Documents.Commands;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Turns the failed message documents RavenDB streams into migration rows.
/// </summary>
class FailedMessageStream(RavenReadOnlySourceLifecycle lifecycle)
{
    /// <summary>
    /// Builds the function that turns each streamed failed message into a row. The function returns null for a message whose
    /// status is not in <paramref name="statuses" />, and throws <see cref="InvalidOperationException" /> for a status that no
    /// failed message category copies.
    /// </summary>
    public Func<StreamResult<FailedMessage>, MigrationRow?> Project(IReadOnlyCollection<FailedMessageStatus> statuses) =>
        result =>
        {
            var status = result.Document.Status;

            if (!CopiedStatuses.Contains(status))
            {
                throw new InvalidOperationException($"The failed message document '{result.Id}' has the status value {(int)status}, which no failed message category copies, so it would be left behind without a skip to show for it.");
            }

            if (!statuses.Contains(status))
            {
                return null;
            }

            RestoreMetadataTypes(result.Document);

            return new MigrationRow(
                result.Id,
                result.Document,
                new Dictionary<string, object?>
                {
                    [MigrationMetadataKeys.StatusChangedAt] = StatusChangedAt(result.Document, ReadMetadataDate(result, Constants.Documents.Metadata.Expires), lifecycle.Settings.ErrorRetentionPeriod),
                    [MigrationMetadataKeys.LastModified] = ReadMetadataDate(result, Constants.Documents.Metadata.LastModified)
                });
        };

    /// <summary>
    /// Every status some failed message category copies.
    /// </summary>
    public static readonly FailedMessageStatus[] CopiedStatuses =
        [.. MigrationCategoryRegistry.RetryIssuedStatuses, .. MigrationCategoryRegistry.UnresolvedStatuses, .. MigrationCategoryRegistry.ArchivedAndResolvedStatuses];

    /// <summary>
    /// The cursor of a batch from <see cref="ClaimHolders" />, which follows the id of the last retry claim document it walked. It cannot be a
    /// message id, so a resume tells the retry-issued messages apart from the claim holders that come after them.
    /// </summary>
    public const string ClaimHolderCursorPrefix = "claim-holder:";

    /// <summary>
    /// Reads the messages that hold a retry claim and are not retry-issued, whatever their status, after the claim document id in
    /// <paramref name="resumeAfter" />, each row carrying <see cref="MigrationMetadataKeys.RetryClaimHeld" /> as true. The claim documents
    /// stream in id order through <see cref="RavenDocumentStream.ByPrefix{TDocument}" />, so memory stays bounded by the batch size.
    /// A claim whose message is gone makes no row. Every batch's cursor is <see cref="ClaimHolderCursorPrefix" /> and the last claim document id it walked.
    /// </summary>
    public async IAsyncEnumerable<MigrationBatch> ClaimHolders(string categoryId, string? resumeAfter, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var claimPrefix = RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId("");
        var project = Project(CopiedStatuses);

        // The claim document stands in as the row so the stream can batch it; the message it names replaces it below.
        await foreach (var claims in RavenDocumentStream.ByPrefix<FailedMessageRetry>(
            lifecycle, categoryId, lifecycle.Settings.DatabaseName, claimPrefix, resumeAfter, batchSize, RavenDocumentStream.MaxDocumentsWalkedPerBatch,
            result => new MigrationRow(result.Id, result.Document, new Dictionary<string, object?>()), cancellationToken))
        {
            string[] messageIds = [.. claims.Rows.Select(claim => FailedMessageIdGenerator.MakeDocumentId(claim.SourceId[claimPrefix.Length..]))];
            var found = new Dictionary<string, MigrationRow>(StringComparer.OrdinalIgnoreCase);

            if (messageIds.Length > 0)
            {
                using var session = lifecycle.OpenSession(lifecycle.Settings.DatabaseName);

                // Queried by id on the collection, which builds no index. A load would not do, because a session that does not track cannot hand back the document's metadata.
                var query = session.Advanced.AsyncDocumentQuery<FailedMessage>(collectionName: FailedMessageIdGenerator.CollectionName).WhereIn("id()", messageIds.Cast<object>());
                await using var results = await session.Advanced.StreamAsync(query, cancellationToken);

                // The metadata is valid only while the stream is on its result, so each result becomes a row as it arrives.
                while (await results.MoveNextAsync())
                {
                    // A retry-issued message is already in the batches before these.
                    if (results.Current.Document.Status != FailedMessageStatus.RetryIssued)
                    {
                        var row = project(results.Current)!;
                        found[results.Current.Id] = row with { Metadata = new Dictionary<string, object?>(row.Metadata) { [MigrationMetadataKeys.RetryClaimHeld] = true } };
                    }
                }
            }

            // A claim can outlive its message, which then has no row.
            yield return new MigrationBatch([.. messageIds.Where(found.ContainsKey).Select(id => found[id])], ClaimHolderCursorPrefix + claims.Cursor);
        }
    }

    /// <summary>
    /// Returns the batch with every retry-issued row carrying <see cref="MigrationMetadataKeys.RetryClaimHeld" />, true when
    /// RavenDB still holds a retry claim for the message. It looks up every claim in the batch with one request, and returns a
    /// batch with no retry-issued row as it is.
    /// </summary>
    public async Task<MigrationBatch> ResolveRetryClaims(MigrationBatch batch, CancellationToken cancellationToken = default)
    {
        string[] claimIds = [.. batch.Rows.Where(IsRetryIssued).Select(ClaimId)];

        if (claimIds.Length == 0)
        {
            return batch;
        }

        using var session = lifecycle.OpenSession(lifecycle.Settings.DatabaseName);

        var claims = await session.LoadAsync<FailedMessageRetry>(claimIds.Distinct(StringComparer.OrdinalIgnoreCase), cancellationToken);
        var held = claims.Where(claim => claim.Value is not null).Select(claim => claim.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return batch with
        {
            Rows = [.. batch.Rows.Select(row => IsRetryIssued(row)
                ? row with { Metadata = new Dictionary<string, object?>(row.Metadata) { [MigrationMetadataKeys.RetryClaimHeld] = held.Contains(ClaimId(row)) } }
                : row)]
        };
    }

    static bool IsRetryIssued(MigrationRow row) => ((FailedMessage)row.Document).Status == FailedMessageStatus.RetryIssued;

    static string ClaimId(MigrationRow row) => RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId(((FailedMessage)row.Document).UniqueMessageId);

    // The translator checks TimeSent, SendingEndpoint and ReceivingEndpoint by exact type, but RavenDB can hand them back as text or untyped objects.
    // Only the newest attempt is fixed because the translator reads no other, and a TimeSent that is not a date is left as it is for the translator to skip.
    internal static void RestoreMetadataTypes(FailedMessage document)
    {
        if (document.ProcessingAttempts is not { Count: > 0 } || document.ProcessingAttempts[^1] is not { MessageMetadata: { } metadata } newest)
        {
            return;
        }

        if (metadata.GetValueOrDefault("TimeSent") is string timeSent
            && DateTime.TryParse(timeSent, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            metadata["TimeSent"] = parsed;
        }

        if (newest.Headers is not { } headers)
        {
            return;
        }

        RestoreEndpoint(metadata, "SendingEndpoint", () => MigrationEndpointDetailsParser.SendingEndpoint(headers));
        RestoreEndpoint(metadata, "ReceivingEndpoint", () => MigrationEndpointDetailsParser.ReceivingEndpoint(headers));
    }

    static void RestoreEndpoint(Dictionary<string, object> metadata, string key, Func<EndpointDetails?> parseHeaders)
    {
        if (metadata.GetValueOrDefault(key) is null or EndpointDetails)
        {
            return;
        }

        // Storing null is deliberate, because the translator reads it as no endpoint, the same as when ingestion stored none.
        metadata[key] = parseHeaders()!;
    }

    // For an archived or resolved message RavenDB stores only when it expires, so the time its status
    // changed is that expiry less the ErrorRetentionPeriod the source set it with.
    internal static DateTime? StatusChangedAt(FailedMessage document, DateTime? expires, TimeSpan sourceErrorRetentionPeriod)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceErrorRetentionPeriod, TimeSpan.Zero, "ErrorRetentionPeriod");

        return document.Status is FailedMessageStatus.Resolved or FailedMessageStatus.Archived && expires is { } deadline
            ? AsUtc(deadline) - sourceErrorRetentionPeriod
            : document.ProcessingAttempts?.Where(attempt => attempt is not null).MaxBy(attempt => attempt.AttemptedAt) is { } newest ? AsUtc(newest.AttemptedAt) : null;
    }

    // RavenDB stores times in UTC, so a time read back with no kind is already UTC and must not be shifted by the local time zone.
    static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    internal static DateTime? ReadMetadataDate<TDocument>(StreamResult<TDocument> result, string key) =>
        result.Metadata.TryGetValue(key, out var value) && value != null
            ? AsUtc(DateTime.Parse(value.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
            : null;
}
