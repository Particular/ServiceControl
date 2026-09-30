namespace ServiceControl.Persistence.DataMigration;

/// <summary>The migration's setting names, relative to the instance's settings root, and their defaults. <see cref="MigrationEngineOptions"/> explains the tuning ones.</summary>
public static class MigrationSettings
{
    public const string ThrottlePauseMillisecondsKey = "Migration/ThrottlePauseMilliseconds";
    public const string HaltThresholdPercentKey = "Migration/HaltThresholdPercent";
    public const string HaltThresholdMinimumKey = "Migration/HaltThresholdMinimum";
    /// <summary>A comma-separated list of the optional categories to copy, such as "EventLog, CustomChecks".</summary>
    public const string OptionalCategoriesKey = "Migration/OptionalCategories";

    public const int DefaultThrottlePauseMilliseconds = 100;
    public const int DefaultHaltThresholdPercent = 5;
    public const int DefaultHaltThresholdMinimum = 100;
}
