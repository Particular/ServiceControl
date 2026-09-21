namespace ServiceControl.Persistence.DataMigration;

/// <summary>
/// The migration's setting names, relative to the instance's settings root, and their defaults.
/// <see cref="MigrationEngineOptions"/> explains the tuning ones.
/// </summary>
public static class MigrationSettings
{
    public const string ThrottlePauseMillisecondsKey = "Migration/ThrottlePauseMilliseconds";
    public const string HaltThresholdPercentKey = "Migration/HaltThresholdPercent";
    public const string HaltThresholdMinimumKey = "Migration/HaltThresholdMinimum";
    /// <summary>
    /// A comma-separated list of the optional categories to copy, such as "EventLog, CustomChecks".
    /// </summary>
    public const string OptionalCategoriesKey = "Migration/OptionalCategories";
    /// <summary>
    /// Which persister holds the old data being copied from.
    /// </summary>
    public const string SourcePersistenceTypeKey = "Migration/SourcePersistenceType";
    /// <summary>
    /// Turns the migration on: the required copy runs before the host opens.
    /// </summary>
    public const string EnabledKey = "Migration/Enabled";
    /// <summary>
    /// Accepts permanent loss on an instance that has already served traffic on the target.
    /// </summary>
    public const string AllowIncompleteExitKey = "Migration/AllowIncompleteExit";

    public const int DefaultThrottlePauseMilliseconds = 100;
    public const int DefaultHaltThresholdPercent = 5;
    public const int DefaultHaltThresholdMinimum = 100;
    /// <summary>
    /// RavenDB is the only supported source currently.
    /// </summary>
    public const string DefaultSourcePersistenceType = "RavenDB";
    public const bool DefaultEnabled = false;
    public const bool DefaultAllowIncompleteExit = false;
}
