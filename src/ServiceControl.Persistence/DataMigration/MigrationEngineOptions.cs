namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using ServiceControl.Configuration;

/// <summary>The engine's tuning, read from settings once at startup.</summary>
/// <param name="ThrottlePause">How long to wait between batches of optional data, so normal work isn't slowed down. Zero means don't wait.</param>
/// <param name="HaltThresholdPercent">A category halts when more than this percent of the rows handled in this run were skipped, and the minimum below is also passed.</param>
/// <param name="HaltThresholdMinimum">A category never halts until more than this many rows were skipped in this run, so a few bad rows can't stop a small category.</param>
/// <param name="SelectedOptionalCategoryIds">The optional categories to copy. Required categories are always copied.</param>
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
