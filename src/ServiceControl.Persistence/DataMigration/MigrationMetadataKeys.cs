namespace ServiceControl.Persistence.DataMigration;

/// <summary>
/// Keys for the extra values a source attaches to a <see cref="MigrationRow" />. The source works each value out
/// itself, so the target never needs to know how the source stores it.
/// </summary>
public static class MigrationMetadataKeys
{
    /// <summary>
    /// The instant a failed message moved to its current status, as a UTC <see cref="System.DateTime" />. The retention sweep
    /// ages a row out from this instant.
    /// </summary>
    public const string StatusChangedAt = "StatusChangedAt";

    /// <summary>
    /// The time the source last changed the row's document, as a UTC <see cref="System.DateTime" />.
    /// </summary>
    public const string LastModified = "LastModified";

    /// <summary>
    /// The time the source last stored a failed error import, as a UTC <see cref="System.DateTime" />.
    /// RavenDB keeps no failure time, so the target stores this one as the failure time, and replay runs the oldest first by it.
    /// </summary>
    public const string FailedAt = "FailedAt";

    /// <summary>
    /// A <see cref="bool" /> that is true when the row is a result of a check the source's own storage runs about itself.
    /// Rows that are not such a result carry no value.
    /// </summary>
    public const string SourceStorageCheck = "SourceStorageCheck";

    /// <summary>
    /// A <see cref="bool" /> on every retry-issued failed message. It is true when the source still holds a retry claim
    /// for the message, and false when the source had already released it. Other rows carry no value.
    /// </summary>
    public const string RetryClaimHeld = "RetryClaimHeld";
}
