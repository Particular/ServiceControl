namespace ServiceControl.Persistence.DataMigration;

/// <summary>
/// Why one row was not copied. Every skipped row carries one, so the verify command can account for the
/// difference between the two databases. <see cref="MigrationSkipReasonExtensions.IsBenign" /> says which of
/// these are no loss.
/// </summary>
public enum MigrationSkipReason
{
    /// <summary>
    /// Every attempt to read the message body from the source failed, so the whole message was left out rather than
    /// written without one. A retry reads it again once the cause is fixed.
    /// </summary>
    BodyUnreadable,

    /// <summary>
    /// The row is older than the retention period, so the instance would have deleted it soon anyway.
    /// </summary>
    PastRetention,

    /// <summary>
    /// The source row has no value for something the target column requires.
    /// </summary>
    RequiredValueMissing,

    /// <summary>
    /// The group comment is null or blank, and the product keeps no row for a blank comment.
    /// </summary>
    BlankGroupComment,

    /// <summary>
    /// The document's unique message id is not a GUID, and the row's key has to be one.
    /// </summary>
    UnparseableUniqueMessageId,

    /// <summary>
    /// The document holds no processing attempt, and every column that describes the failure comes from one.
    /// </summary>
    NoProcessingAttempts,

    /// <summary>
    /// A value is longer than its target column allows, so storing the row would fail the whole insert.
    /// </summary>
    ValueTooLong,

    /// <summary>
    /// A value the target needs is present but holds the wrong type, so the row cannot be built from it.
    /// </summary>
    MalformedValue,

    /// <summary>
    /// The source holds the message but no body for it, so the whole message was left out rather than written
    /// without one.
    /// </summary>
    BodyMissing,

    /// <summary>
    /// A reason a newer build wrote and this one cannot name, so an older host still starts instead of throwing.
    /// Nothing in this build writes it. It counts as a fault, because harmless would let this build settle a
    /// category Done over rows the newer build lost; a false Failed from it is cleared by one retry.
    /// </summary>
    Unknown
}
