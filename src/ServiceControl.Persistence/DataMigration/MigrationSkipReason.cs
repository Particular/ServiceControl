namespace ServiceControl.Persistence.DataMigration;

/// <summary>
/// Why one row was not copied. Every skipped row carries one, so the verify command can account for the
/// difference between the two databases. <see cref="MigrationSkipReasonExtensions.IsBenign" /> says which of
/// these are no loss.
/// </summary>
public enum MigrationSkipReason
{
    /// <summary>The message body could not be read from the source, after the engine had tried more than once.</summary>
    BodyUnreadable,

    /// <summary>The row is older than the retention period, so the instance would have deleted it soon anyway.</summary>
    PastRetention,

    /// <summary>The source row has no value for something the target column requires.</summary>
    RequiredValueMissing,

    /// <summary>
    /// The group comment is null or blank, and the product keeps no row for a blank comment.
    /// </summary>
    BlankGroupComment,

    /// <summary>
    /// A reason a newer build wrote and this one cannot name, so an older host still starts instead of throwing.
    /// Nothing in this build writes it. It counts as a fault, because harmless would let this build settle a
    /// category Done over rows the newer build lost; a false Failed from it is cleared by one retry.
    /// </summary>
    Unknown
}
