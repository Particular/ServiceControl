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

    /// <summary>The row names an endpoint the target does not know, so what it holds would be deleted after the cutover anyway.</summary>
    EndpointNotKnown,

    /// <summary>
    /// Nothing ever writes this. A reason only a newer build knows reads back as Unknown, so an older host still
    /// starts instead of throwing.
    /// </summary>
    Unknown
}
