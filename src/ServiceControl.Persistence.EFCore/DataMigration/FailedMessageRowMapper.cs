namespace ServiceControl.Persistence.EFCore.DataMigration;

using System.Text.Json;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.EntityConfigurations;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;
using ServiceControl.Persistence.EFCore.Infrastructure;

static class FailedMessageRowMapper
{
    public static FailedMessageRowMapping Map(MigrationRow source)
    {
        var document = (FailedMessage)source.Document;

        if (!Guid.TryParse(document.UniqueMessageId, out var uniqueMessageId))
        {
            return new FailedMessageRowMapping.Skipped(
                MigrationSkipReason.UnparseableUniqueMessageId,
                $"UniqueMessageId '{document.UniqueMessageId}' on document '{source.SourceId}' is not a GUID and the target column is a uniqueidentifier");
        }

        if (document.ProcessingAttempts is not { Count: > 0 })
        {
            return new FailedMessageRowMapping.Skipped(
                MigrationSkipReason.NoProcessingAttempts,
                $"Document '{source.SourceId}' has no processing attempts, so no attempt supplies the required columns");
        }

        if (document.ProcessingAttempts[^1] is not { } newest)
        {
            return new FailedMessageRowMapping.Skipped(
                MigrationSkipReason.RequiredValueMissing,
                $"The newest attempt on document '{source.SourceId}' is null, so no attempt supplies the required columns");
        }

        if (MissingFrom(newest) is { } missing)
        {
            return new FailedMessageRowMapping.Skipped(
                MigrationSkipReason.RequiredValueMissing,
                $"The newest attempt on document '{source.SourceId}' has no {missing}, which the row's columns are built from");
        }

        if (MalformedIn(newest.MessageMetadata) is { } malformed)
        {
            return new FailedMessageRowMapping.Skipped(
                MigrationSkipReason.MalformedValue,
                $"The newest attempt on document '{source.SourceId}' holds a {malformed.Found.FullName} as its {malformed.Key} metadata, where the row needs a {malformed.Expected.FullName}: '{Shown(malformed.Value)}'");
        }

        // An attempt stored without FailureDetails reads back as the default object, whose address is empty.
        if (string.IsNullOrEmpty(newest.FailureDetails.AddressOfFailingEndpoint))
        {
            return new FailedMessageRowMapping.Skipped(
                MigrationSkipReason.RequiredValueMissing,
                $"FailedMessages.{nameof(FailedMessageEntity.FailingEndpointAddress)} is NOT NULL and the newest attempt on document '{source.SourceId}' has no AddressOfFailingEndpoint");
        }

        if (newest.FailureDetails.TimeOfFailure == default || newest.AttemptedAt == default)
        {
            return new FailedMessageRowMapping.Skipped(
                MigrationSkipReason.RequiredValueMissing,
                $"The newest attempt on document '{source.SourceId}' has no {(newest.FailureDetails.TimeOfFailure == default ? nameof(newest.FailureDetails.TimeOfFailure) : nameof(newest.AttemptedAt))}, which the row's failure times are built from");
        }

        var metadata = newest.MessageMetadata;
        // The newest attempt passed the checks above, so the list always holds at least one time for Min and Max.
        var timesOfFailure = document.ProcessingAttempts
            .Where(HasRecordedFailure)
            .Select(attempt => attempt.FailureDetails.TimeOfFailure)
            .ToList();
        var sendingEndpoint = (EndpointDetails?)metadata.GetValueOrDefault("SendingEndpoint");
        var receivingEndpoint = (EndpointDetails?)metadata.GetValueOrDefault("ReceivingEndpoint");

        var row = new FailedMessageEntity
        {
            UniqueMessageId = uniqueMessageId,
            Status = document.Status,
            StatusChangedAt = ReadResolvedDate(source, MigrationMetadataKeys.StatusChangedAt),
            LastModified = ReadResolvedDate(source, MigrationMetadataKeys.LastModified),
            NumberOfProcessingAttempts = document.ProcessingAttempts.Count,
            FirstTimeOfFailure = AsUtc(timesOfFailure.Min()),
            LastTimeOfFailure = AsUtc(timesOfFailure.Max()),
            LastAttemptedAt = AsUtc(newest.AttemptedAt),
            MessageId = newest.MessageId,
            MessageType = LastCharactersThatFit(metadata.GetValueOrDefault("MessageType") as string),
            TimeSent = metadata.GetValueOrDefault("TimeSent") is DateTime timeSent ? AsUtc(timeSent) : null,
            ConversationId = metadata.GetValueOrDefault("ConversationId") as string,
            SendingEndpointName = sendingEndpoint?.Name,
            SendingEndpointHostId = sendingEndpoint?.HostId,
            SendingEndpointHost = sendingEndpoint?.Host,
            ReceivingEndpointName = receivingEndpoint?.Name,
            ReceivingEndpointHostId = receivingEndpoint?.HostId,
            ReceivingEndpointHost = receivingEndpoint?.Host,
            ExceptionType = newest.FailureDetails.Exception?.ExceptionType,
            ExceptionMessage = newest.FailureDetails.Exception?.Message,
            IsSystemMessage = metadata.GetValueOrDefault("IsSystemMessage") is true,
            HeadersJson = MessageHeaders.Write(newest.Headers),
            FailingEndpointAddress = newest.FailureDetails.AddressOfFailingEndpoint
        };

        // Null groups and groups with no id are dropped, because GroupId is part of a NOT NULL key.
        IReadOnlyList<FailedMessageGroupEntity> groups =
        [
            .. (document.FailureGroups ?? [])
                .Where(failureGroup => failureGroup is { Id: not null })
                .Select(failureGroup => new FailedMessageGroupEntity
                {
                    FailedMessageUniqueId = uniqueMessageId,
                    GroupId = failureGroup.Id,
                    Title = failureGroup.Title ?? string.Empty,
                    Type = failureGroup.Type ?? string.Empty
                })
        ];

        return new FailedMessageRowMapping.Mapped(row, groups);
    }

    /// <summary>
    /// Fills the four body columns of a mapped row the way ingestion does, letting <see cref="MessageBodyClassifier" />
    /// decide whether the body stays inline or goes to external storage. Returns true when the body goes to external
    /// storage. It changes nothing else on the row and writes no body itself.
    /// </summary>
    public static bool SetBody(FailedMessageEntity row, IReadOnlyDictionary<string, string> headers, MigrationBody body, int maxBodySizeToStore)
    {
        var (bodyText, storeExternally) = MessageBodyClassifier.Classify(headers, body.Content, maxBodySizeToStore);

        row.BodyText = bodyText;
        row.BodyStoredExternally = storeExternally;
        row.BodySize = body.Content.Length;
        row.BodyContentType = body.ContentType;

        return storeExternally;
    }

    static string? MissingFrom(FailedMessage.ProcessingAttempt attempt) =>
        attempt.FailureDetails is null ? nameof(attempt.FailureDetails)
        : attempt.MessageMetadata is null ? nameof(attempt.MessageMetadata)
        : attempt.Headers is null ? nameof(attempt.Headers)
        : null;

    // An attempt stored without FailureDetails reads back as a default object with an empty address and a time of failure set to when it was read.
    static bool HasRecordedFailure(FailedMessage.ProcessingAttempt? attempt) =>
        attempt is { FailureDetails: { AddressOfFailingEndpoint: { Length: > 0 } } details } && details.TimeOfFailure != default;

    // A missing value becomes null, just as RavenDB's own read path shows it, so only a value of the wrong type needs a skip.
    static (string Key, Type Found, Type Expected, object Value)? MalformedIn(Dictionary<string, object> metadata)
    {
        foreach (var (key, expected) in TypedMetadata)
        {
            if (metadata.GetValueOrDefault(key) is { } value && !expected.IsInstanceOfType(value))
            {
                return (key, value.GetType(), expected, value);
            }
        }

        return null;
    }

    // Every metadata value the row reads by type, so a value of another type is skipped rather than copied as null or false.
    static readonly (string Key, Type Expected)[] TypedMetadata =
    [
        ("TimeSent", typeof(DateTime)),
        ("MessageType", typeof(string)),
        ("ConversationId", typeof(string)),
        ("IsSystemMessage", typeof(bool))
    ];

    const int MaxShownValueLength = 100;

    // Cut short because a stored value can be any length and the skip detail is one log line.
    static string Shown(object value)
    {
        var text = value as string ?? JsonSerializer.Serialize(value);

        return text.Length > MaxShownValueLength ? $"{text[..MaxShownValueLength]}..." : text;
    }

    // Throws rather than make up a date, because a made-up StatusChangedAt or LastModified would give the row a retention or modification time it never had.
    static DateTime ReadResolvedDate(MigrationRow source, string key) =>
        source.Metadata.GetValueOrDefault(key) switch
        {
            DateTime value => AsUtc(value),
            null => throw new InvalidOperationException($"Row '{source.SourceId}' arrived without a {key} value, which the source resolves before a failed message crosses the seam"),
            var other => throw new InvalidOperationException($"Row '{source.SourceId}' carries a {other.GetType().FullName} as its {key} value, where the source resolves a {typeof(DateTime).FullName} before a failed message crosses the seam")
        };

    // Npgsql only writes UTC to a timestamp with time zone column, and a value whose kind is Unspecified is already UTC, so it must not be shifted.
    // Kept apart from the RavenDB source's copy because this project does not reference it.
    static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    // Uses the same length limit that ingestion applies to the MessageType column.
    static string? LastCharactersThatFit(string? value) =>
        value is { Length: > ColumnLengths.ShortTextLength } ? value[^ColumnLengths.ShortTextLength..] : value;
}
