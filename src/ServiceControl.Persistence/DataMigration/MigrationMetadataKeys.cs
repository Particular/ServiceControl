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
}
