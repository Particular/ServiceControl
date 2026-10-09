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
    /// How far back the event log copy goes, as a time span. Unset means the event retention period, and zero
    /// turns the category off.
    /// </summary>
    public const string EventLogWindowKey = "Migration/EventLogWindow";
    /// <summary>
    /// How far back the archived and resolved failed messages copy goes, as a time span. Unset means the error
    /// retention period, and zero turns the category off.
    /// </summary>
    public const string ArchivedAndResolvedFailedMessagesWindowKey = "Migration/ArchivedAndResolvedFailedMessagesWindow";
    /// <summary>
    /// Turns the migration on: the required copy runs before the host opens.
    /// </summary>
    public const string EnabledKey = "Migration/Enabled";

    public const int DefaultThrottlePauseMilliseconds = 100;
    public const int DefaultHaltThresholdPercent = 5;
    public const int DefaultHaltThresholdMinimum = 100;
    public const bool DefaultEnabled = false;
}
