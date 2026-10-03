namespace ServiceControl.Persistence.DataMigration;

/// <summary>
/// Whether this instance is still part way through copying its old database in.
/// </summary>
public interface IMigrationState
{
    /// <summary>
    /// True while any required category is unfinished. Services that delete or overwrite copied data stand down while it
    /// is true. Optional categories never make it true, because they copy beside those services.
    /// </summary>
    bool AnyCategoryIncomplete { get; }
}
