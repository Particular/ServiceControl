namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using ServiceControl.Configuration;

/// <summary>The engine's tuning: the pause between optional batches, when a category halts, and which optional categories to copy.</summary>
public sealed record MigrationEngineOptions(
    TimeSpan ThrottlePause,
    int HaltThresholdPercent,
    int HaltThresholdMinimum,
    IReadOnlyCollection<string> SelectedOptionalCategoryIds)
{
    public static readonly TimeSpan DefaultBodyRetryBackoff = TimeSpan.FromMilliseconds(200);

    /// <summary>How long to wait before trying again to read a message body that failed. Not read from settings.</summary>
    public TimeSpan BodyRetryBackoff { get; init; } = DefaultBodyRetryBackoff;

    /// <summary>Reads the options from settings. Throws if the optional categories setting names one that doesn't exist or isn't optional.</summary>
    public static MigrationEngineOptions FromSettings(SettingsRootNamespace settingsRootNamespace)
    {
        var throttleMilliseconds = SettingsReader.Read(settingsRootNamespace, MigrationSettings.ThrottlePauseMillisecondsKey, MigrationSettings.DefaultThrottlePauseMilliseconds);
        var haltPercent = SettingsReader.Read(settingsRootNamespace, MigrationSettings.HaltThresholdPercentKey, MigrationSettings.DefaultHaltThresholdPercent);
        var haltMinimum = SettingsReader.Read(settingsRootNamespace, MigrationSettings.HaltThresholdMinimumKey, MigrationSettings.DefaultHaltThresholdMinimum);
        var optionalCategories = SettingsReader.Read(settingsRootNamespace, MigrationSettings.OptionalCategoriesKey, string.Empty);

        var selectedIds = optionalCategories
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        var unknown = selectedIds
            .Where(id => MigrationCategoryRegistry.Find(id) is not { Kind: MigrationCategoryKind.Optional })
            .ToArray();

        if (unknown.Length > 0)
        {
            throw new InvalidOperationException(
                $"{MigrationSettings.OptionalCategoriesKey} names categories that do not exist or are not optional: {string.Join(", ", unknown)}");
        }

        return new MigrationEngineOptions(TimeSpan.FromMilliseconds(throttleMilliseconds), haltPercent, haltMinimum, selectedIds);
    }
}
