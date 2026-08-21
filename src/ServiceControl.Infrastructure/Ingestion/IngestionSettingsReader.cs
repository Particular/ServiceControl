namespace ServiceControl.Infrastructure.Ingestion;

using System;
using Microsoft.Extensions.Logging;
using NLog.Common;
using ServiceControl.Configuration;

/// <summary>
/// Reads and validates the settings that tune an <see cref="IngestionPipeline" />. Each instance
/// names its own after what it ingests, so the caller supplies the name and only the bounds and the
/// messages are shared.
/// </summary>
public static class IngestionSettingsReader
{
    /// <summary>
    /// The most messages one write handles. Absent leaves it to the transport's concurrency, which
    /// is as many messages as can be waiting to be written at any one time.
    /// </summary>
    public static int? ReadBatchSize(SettingsRootNamespace settingsNamespace, string name, bool validateConfiguration) =>
        ReadInt(settingsNamespace, name, validateConfiguration, minimum: 1, maximum: MaximumBatchSize);

    /// <summary>
    /// How many batches are written at once. Absent leaves it to the storage, which is
    /// <see cref="DefaultMaxParallelWriters" /> where batches are safe to interleave and one where
    /// they are not.
    /// </summary>
    public static int? ReadMaxParallelWriters(SettingsRootNamespace settingsNamespace, string name, bool validateConfiguration) =>
        ReadInt(settingsNamespace, name, validateConfiguration, minimum: 1, maximum: MaximumParallelWriters);

    /// <summary>
    /// How long a batch that is not yet full waits for more messages. Absent does not wait at all.
    /// </summary>
    public static TimeSpan ReadBatchTimeout(SettingsRootNamespace settingsNamespace, string name, bool validateConfiguration)
    {
        if (!SettingsReader.TryRead<string>(settingsNamespace, name, out var value))
        {
            return TimeSpan.Zero;
        }

        if (!TimeSpan.TryParse(value, out var timeout))
        {
            throw Invalid($"{name} setting is invalid, please make sure it is a TimeSpan.");
        }

        if (validateConfiguration && (timeout < TimeSpan.Zero || timeout > MaximumBatchTimeout))
        {
            throw Invalid($"{name} setting is invalid, value should be between zero and {MaximumBatchTimeout}.");
        }

        return timeout;
    }

    /// <summary>
    /// Settles how many writers a pipeline actually gets. A storage whose batches are not safe to
    /// interleave holds it at one whatever is configured, and says so when that overrules a
    /// deliberate setting rather than a default.
    /// </summary>
    public static int ResolveMaxParallelWriters(int? configured, bool storageSupportsConcurrentBatches, string settingName, ILogger logger)
    {
        if (storageSupportsConcurrentBatches)
        {
            return configured ?? DefaultMaxParallelWriters;
        }

        if (configured > 1)
        {
            logger.LogWarning(
                "{SettingName} is set to {ConfiguredWriters}, but the configured storage writes ingestion batches one at a time. One writer is used.",
                settingName, configured);
        }

        return 1;
    }

    static int? ReadInt(SettingsRootNamespace settingsNamespace, string name, bool validateConfiguration, int minimum, int maximum)
    {
        if (!SettingsReader.TryRead<int>(settingsNamespace, name, out var value))
        {
            return null;
        }

        if (validateConfiguration && (value < minimum || value > maximum))
        {
            throw Invalid($"{name} setting is invalid, value should be between {minimum} and {maximum}.");
        }

        return value;
    }

    // Logged as well as thrown because a bad setting stops the instance before logging is configured
    static Exception Invalid(string message)
    {
        InternalLogger.Fatal(message);

        return new Exception(message);
    }

    /// <summary>
    /// What a storage whose batches are safe to interleave gets when nothing is configured.
    /// </summary>
    public const int DefaultMaxParallelWriters = 4;

    const int MaximumBatchSize = 1000;
    const int MaximumParallelWriters = 16;
    static readonly TimeSpan MaximumBatchTimeout = TimeSpan.FromSeconds(5);
}