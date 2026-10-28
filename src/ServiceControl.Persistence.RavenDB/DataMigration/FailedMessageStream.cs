#nullable enable
namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    /// status is not in <paramref name="statuses" />, and throws <see cref="InvalidOperationException" /> for a status that neither
    /// failed message category copies.
    /// </summary>
    public Func<StreamResult<FailedMessage>, MigrationRow?> Project(IReadOnlyCollection<FailedMessageStatus> statuses) =>
        result =>
        {
            var status = result.Document.Status;

            if (!MigrationCategoryRegistry.UnresolvedAndRetryIssuedStatuses.Contains(status) && !MigrationCategoryRegistry.ArchivedAndResolvedStatuses.Contains(status))
            {
                throw new InvalidOperationException($"The failed message document '{result.Id}' has the status value {(int)status}, which neither failed message category copies, so it would be left behind without a skip to show for it.");
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

    // The mapper checks TimeSent, SendingEndpoint and ReceivingEndpoint by exact type, but RavenDB can hand them back as text or untyped objects.
    // Only the newest attempt is fixed because the mapper reads no other, and a TimeSent that is not a date is left as it is for the mapper to skip.
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

        // Storing null is deliberate, because the mapper reads it as no endpoint, the same as when ingestion stored none.
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

    static DateTime? ReadMetadataDate(StreamResult<FailedMessage> result, string key) =>
        result.Metadata.TryGetValue(key, out var value) && value != null
            ? AsUtc(DateTime.Parse(value.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
            : null;
}
