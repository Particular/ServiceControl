namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using ServiceControl.Configuration;

/// <summary>
/// The engine's tuning: the pause between optional batches, when a category halts, and which optional categories
/// to copy and how far back.
/// </summary>
/// <param name="ThrottlePause">How long to wait between batches of an optional category, which is what keeps the background copy off the instance's back. Zero waits not at all.</param>
/// <param name="HaltThresholdPercent">The share of skipped rows, as a percentage, that stops a category.</param>
/// <param name="HaltThresholdMinimum">The number of skipped rows that has to be passed before the percentage counts, so a handful of bad rows in a small category is not a halt.</param>
/// <param name="SelectedOptionalCategoryIds">The optional categories to copy. An optional category outside this list is never copied.</param>
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
    /// How far back the event log copy goes, measured against when each event was raised.
    /// </summary>
    public TimeSpan EventLogWindow { get; init; }

    /// <summary>
    /// How far back the archived and resolved failed messages copy goes, measured against when each message was
    /// archived or resolved.
    /// </summary>
    public TimeSpan ArchivedAndResolvedFailedMessagesWindow { get; init; }

    /// <summary>
    /// Reads the options from the instance's settings. Anything not set takes the default from
    /// <see cref="MigrationSettings" />, except the two windows, which default to the retention periods passed in.
    /// An optional category is selected when its window is greater than zero.
    /// </summary>
    /// <param name="settingsRootNamespace">The settings root the migration keys are read under.</param>
    /// <param name="eventRetentionPeriod">The instance's event retention period, used as the event log window when none is set.</param>
    /// <param name="errorRetentionPeriod">The instance's error retention period, used as the archived and resolved window when none is set.</param>
    /// <exception cref="InvalidOperationException">A window setting is not a time span of zero or more, which the operator has to see before the copy starts.</exception>
    public static MigrationEngineOptions FromSettings(SettingsRootNamespace settingsRootNamespace, TimeSpan eventRetentionPeriod, TimeSpan errorRetentionPeriod)
    {
        var throttleMilliseconds = SettingsReader.Read(settingsRootNamespace, MigrationSettings.ThrottlePauseMillisecondsKey, MigrationSettings.DefaultThrottlePauseMilliseconds);
        var haltPercent = SettingsReader.Read(settingsRootNamespace, MigrationSettings.HaltThresholdPercentKey, MigrationSettings.DefaultHaltThresholdPercent);
        var haltMinimum = SettingsReader.Read(settingsRootNamespace, MigrationSettings.HaltThresholdMinimumKey, MigrationSettings.DefaultHaltThresholdMinimum);
        var eventLogWindow = ReadWindow(settingsRootNamespace, MigrationSettings.EventLogWindowKey, eventRetentionPeriod);
        var archivedAndResolvedWindow = ReadWindow(settingsRootNamespace, MigrationSettings.ArchivedAndResolvedFailedMessagesWindowKey, errorRetentionPeriod);

        var selectedIds = new List<string>();

        if (eventLogWindow > TimeSpan.Zero)
        {
            selectedIds.Add(MigrationCategoryIds.EventLog);
        }

        if (archivedAndResolvedWindow > TimeSpan.Zero)
        {
            selectedIds.Add(MigrationCategoryIds.ArchivedAndResolvedFailedMessages);
        }

        return new MigrationEngineOptions(TimeSpan.FromMilliseconds(throttleMilliseconds), haltPercent, haltMinimum, selectedIds)
        {
            EventLogWindow = eventLogWindow,
            ArchivedAndResolvedFailedMessagesWindow = archivedAndResolvedWindow
        };
    }

    static TimeSpan ReadWindow(SettingsRootNamespace settingsRootNamespace, string key, TimeSpan defaultWindow)
    {
        var value = SettingsReader.Read<string>(settingsRootNamespace, key);

        if (value is null)
        {
            return defaultWindow;
        }

        if (!TimeSpan.TryParse(value, out var window) || window < TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"{key} is '{value}', which is not a time span of zero or more. Set it like a retention period, such as 7.00:00:00 for seven days, or to 0 to leave that category behind.");
        }

        return window;
    }
}
