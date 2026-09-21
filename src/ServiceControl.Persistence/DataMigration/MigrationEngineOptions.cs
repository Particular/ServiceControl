namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using ServiceControl.Configuration;

/// <summary>The engine's tuning: the pause between optional batches, when a category halts, and which optional categories to copy.</summary>
/// <param name="ThrottlePause">How long to wait between batches of an optional category, which is what keeps the background copy off the instance's back. Zero waits not at all.</param>
/// <param name="HaltThresholdPercent">The share of skipped rows, as a percentage, that stops a category.</param>
/// <param name="HaltThresholdMinimum">The number of skipped rows that has to be passed before the percentage counts, so a handful of bad rows in a small category is not a halt.</param>
/// <param name="SelectedOptionalCategoryIds">The optional categories the operator asked for. An optional category outside this list is never copied.</param>
public sealed record MigrationEngineOptions(
    TimeSpan ThrottlePause,
    int HaltThresholdPercent,
    int HaltThresholdMinimum,
    IReadOnlyCollection<string> SelectedOptionalCategoryIds)
{
    public static readonly TimeSpan DefaultBodyRetryBackoff = TimeSpan.FromMilliseconds(200);

    /// <summary>How long to wait before trying again to read a message body that failed. Not read from settings.</summary>
    public TimeSpan BodyRetryBackoff { get; init; } = DefaultBodyRetryBackoff;

    /// <summary>
    /// Reads the options from the instance's settings. Anything not set takes the default from
    /// <see cref="MigrationSettings" />.
    /// </summary>
    /// <exception cref="InvalidOperationException">The optional categories setting names a category that does not exist or is not optional, which is a typo the operator has to see before the copy starts.</exception>
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
